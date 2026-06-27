using Bindito.Core;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Timberborn.AssetSystem;
using Timberborn.Automation;
using Timberborn.AutomationBuildings;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BlueprintSystem;
using Timberborn.Buildings;
using Timberborn.CharacterModelSystem;
using Timberborn.DeteriorationSystem;
using Timberborn.EnterableSystem;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.ModManagerScene;
using Timberborn.NeedSystem;
using Timberborn.PrioritySystem;
using Timberborn.SlotSystem;
using Timberborn.StatusSystem;
using Timberborn.TemplateInstantiation;
using Timberborn.WorkSystem;
using UnityEngine;

namespace Calloatti.BotStorage
{
  public class BotStorageModStarter : IModStarter
  {
    public void StartMod(IModEnvironment modEnvironment)
    {
      new Harmony("calloatti.botstorage").PatchAll();
    }
  }

  public record BotStorageBuildingSpec : ComponentSpec;

  public class BotStorageBuilding : BaseComponent, IAwakableComponent, IUpdatableComponent, IDeletableEntity
  {
    private Enterable _enterable;
    private SlotManager _slotManager;
    private float _easterEggTimer;
    private float _nextEasterEggTime;

    private bool _initializedLoadedBots = false;

    // OPTIMIZATION: Thread-safe O(1) tracking
    public static readonly ConcurrentDictionary<Deteriorable, bool> ProtectedBots = new();

    private static FieldInfo _slotsField;
    private static FieldInfo _transformSlotSpecField;

    private static readonly string[] EasterEggAnimations = { "ForcedIdle", "Sleeping", "Sitting", "CharacterControl", "Stranded" };

    public void Awake()
    {
      _enterable = GetComponent<Enterable>();
      _slotManager = GetComponent<SlotManager>();

      if (_slotsField == null)
      {
        _slotsField = AccessTools.Field(typeof(SlotManager), "_slots");
        _transformSlotSpecField = AccessTools.Field(typeof(TransformSlot), "_transformSlotSpec");
      }

      _enterable.EntererAdded += OnEntererAdded;
      _enterable.EntererRemoved += OnEntererRemoved;

      GetComponent<WorkplacePriority>()?.SetPriority(Timberborn.PrioritySystem.Priority.VeryLow);
      ResetEasterEggTimer();
    }

    public void DeleteEntity()
    {
      if (_enterable != null)
      {
        _enterable.EntererAdded -= OnEntererAdded;
        _enterable.EntererRemoved -= OnEntererRemoved;
      }
    }

    private void OnEntererAdded(object sender, EntererAddedEventArgs e)
    {
      NeedManager nm = e.Enterer.GetComponent<NeedManager>();
      if (nm != null) foreach (var n in nm.NeedSpecs) nm.DisableUpdate(n.Id);

      Deteriorable deteriorable = e.Enterer.GetComponent<Deteriorable>();
      if (deteriorable != null) ProtectedBots.TryAdd(deteriorable, true);
    }

    private void OnEntererRemoved(object sender, EntererRemovedEventArgs e)
    {
      NeedManager nm = e.Enterer.GetComponent<NeedManager>();
      if (nm != null) foreach (var n in nm.NeedSpecs) nm.EnableUpdate(n.Id);

      Deteriorable deteriorable = e.Enterer.GetComponent<Deteriorable>();
      if (deteriorable != null) ProtectedBots.TryRemove(deteriorable, out _);

      CharacterAnimator animator = e.Enterer.GetComponent<CharacterAnimator>();
      if (animator != null)
      {
        foreach (string anim in EasterEggAnimations)
        {
          if (animator.HasParameter(anim))
          {
            animator.SetBool(anim, false);
          }
        }
      }
    }

    public void Update()
    {
      if (!_initializedLoadedBots)
      {
        _initializedLoadedBots = true;
        foreach (var bot in _enterable.EnterersInside)
        {
          Deteriorable deteriorable = bot.GetComponent<Deteriorable>();
          if (deteriorable != null) ProtectedBots.TryAdd(deteriorable, true);
        }
      }

      if (_enterable.NumberOfEnterersInside == 0) return;

      _easterEggTimer += Time.deltaTime;
      if (_easterEggTimer >= _nextEasterEggTime)
      {
        TriggerEasterEgg();
        ResetEasterEggTimer();
      }
    }

    private void ResetEasterEggTimer()
    {
      _easterEggTimer = 0f;
      _nextEasterEggTime = UnityEngine.Random.Range(30f, 120f);
    }

    private void TriggerEasterEgg()
    {
      var bots = _enterable.EnterersInside.ToList();
      if (bots.Count == 0) return;

      var randomBot = bots[UnityEngine.Random.Range(0, bots.Count)];
      GetComponent<MonoBehaviour>().StartCoroutine(EasterEggRoutine(randomBot));
    }

    private IEnumerator EasterEggRoutine(Enterer bot)
    {
      CharacterAnimator animator = bot.GetComponent<CharacterAnimator>();
      if (animator == null) yield break;

      string defaultAnim = "ForcedAPose";

      var slots = _slotsField?.GetValue(_slotManager) as List<ISlot>;
      var botSlot = slots?.OfType<TransformSlot>().FirstOrDefault(s => s.AssignedEnterer == bot);

      if (botSlot != null)
      {
        var spec = _transformSlotSpecField?.GetValue(botSlot) as TransformSlotSpec;
        if (spec != null)
        {
          defaultAnim = spec.Animation;
        }
      }

      string randomAnim = EasterEggAnimations[UnityEngine.Random.Range(0, EasterEggAnimations.Length)];

      if (animator.HasParameter(defaultAnim))
      {
        animator.SetBool(defaultAnim, false);
      }

      if (animator.HasParameter(randomAnim))
      {
        animator.SetBool(randomAnim, true);
      }

      yield return new WaitForSeconds(UnityEngine.Random.Range(3f, 6f));

      if (bot != null && animator != null && _enterable.EnterersInside.Contains(bot))
      {
        if (animator.HasParameter(randomAnim))
        {
          animator.SetBool(randomAnim, false);
        }
        if (animator.HasParameter(defaultAnim))
        {
          animator.SetBool(defaultAnim, true);
        }
      }
    }
  }

  public class BotStorageBannerSetter : BaseComponent, IAwakableComponent, IFinishedStateListener, IDeletableEntity
  {
    private static readonly Color BannerIconColor = new Color(0.33f, 0.33f, 0.33f);
    private readonly IAssetLoader _assetLoader;

    private BlockObject _blockObject;
    private MeshRenderer _meshRenderer;
    private Material _cachedMaterial;

    private static Texture2D _botHeadTexture;
    private static bool _textureLoaded = false;

    private static readonly int IconColorProperty = Shader.PropertyToID("_DetailAlbedoUV2Color");
    private static readonly int TextureProperty = Shader.PropertyToID("_DetailAlbedoMap2");

    public BotStorageBannerSetter(IAssetLoader assetLoader)
    {
      _assetLoader = assetLoader;
    }

    public void Awake()
    {
      _blockObject = GetComponent<BlockObject>();
      BuildingModel component = GetComponent<BuildingModel>();

      if (!_textureLoaded)
      {
        _botHeadTexture = _assetLoader.LoadSafe<Texture2D>("Sprites/Goods/BotHeadIcon");
        _textureLoaded = true;
      }

      Transform bannerTransform = component.FinishedModel.transform.Find("BannerMesh");

      if (bannerTransform != null)
      {
        _meshRenderer = bannerTransform.GetComponent<MeshRenderer>();
      }
      else
      {
        _meshRenderer = component.FinishedModel.GetComponentInChildren<MeshRenderer>();
      }
    }

    public void OnEnterFinishedState()
    {
      if (_meshRenderer != null && _botHeadTexture != null)
      {
        if (_cachedMaterial == null)
        {
          _cachedMaterial = _meshRenderer.material;
        }

        _cachedMaterial.SetTexture(TextureProperty, _botHeadTexture);
        _cachedMaterial.SetColor(IconColorProperty, BannerIconColor);
      }
    }

    public void OnExitFinishedState() { }

    public void DeleteEntity()
    {
      if (_cachedMaterial != null)
      {
        UnityEngine.Object.Destroy(_cachedMaterial);
        _cachedMaterial = null;
      }
    }
  }

  [Context("Game")]
  public class BotStorageConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<BotStorageBuilding>().AsTransient();
      Bind<BotStorageBannerSetter>().AsTransient();
      MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
    }

    private static TemplateModule ProvideTemplateModule()
    {
      var builder = new TemplateModule.Builder();

      builder.AddDecorator<BotStorageBuildingSpec, BotStorageBuilding>();
      builder.AddDecorator<BotStorageBuildingSpec, WaitInsideIdlyWorkplaceBehavior>();
      builder.AddDecorator<BotStorageBuildingSpec, BotStorageBannerSetter>();
      builder.AddDecorator<BotStorageBuildingSpec, PausableBuilding>();

      return builder.Build();
    }
  }

  [HarmonyPatch(typeof(StatusSubject), nameof(StatusSubject.RegisterStatus))]
  public static class PreventUnstaffedStatusPatch
  {
    public static bool Prefix(StatusSubject __instance, StatusToggle statusToggle)
    {
      if (__instance.GetComponent<BotStorageBuilding>() != null)
      {
        string spriteName = statusToggle.StatusSpecification.SpriteName ?? "";

        if (spriteName.Contains("NoUnemployed"))
        {
          return false;
        }
      }
      return true;
    }
  }

  // Restored: The highly optimized O(1) Deterioration Patch
  [HarmonyPatch(typeof(Deteriorable), nameof(Deteriorable.Tick))]
  public static class DeteriorableTickPatch
  {
    public static bool Prefix(Deteriorable __instance)
    {
      if (BotStorageBuilding.ProtectedBots.ContainsKey(__instance))
      {
        return false;
      }
      return true;
    }
  }
}