using System.Collections.Concurrent;
using Timberborn.AssetSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BlueprintSystem;
using Timberborn.Buildings;
using Timberborn.DeteriorationSystem;
using Timberborn.EnterableSystem;
using Timberborn.EntitySystem;
using Timberborn.NeedSystem;
using Timberborn.TemplateSystem;
using Timberborn.WorkSystem;
using UnityEngine;

namespace Calloatti.BotStorage
{
  public record BotStorageBuildingSpec : ComponentSpec;

  public class BotStorageBuilding : BaseComponent, IAwakableComponent, IInitializableEntity, IDeletableEntity
  {
    private Enterable _enterable;

    // OPTIMIZATION: Thread-safe O(1) tracking
    public static readonly ConcurrentDictionary<Deteriorable, bool> ProtectedBots = new();

    public void Awake()
    {
      _enterable = GetComponent<Enterable>();

      _enterable.EntererAdded += OnEntererAdded;
      _enterable.EntererRemoved += OnEntererRemoved;

      GetComponent<WorkplacePriority>()?.SetPriority(Timberborn.PrioritySystem.Priority.VeryLow);
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
    }

    public void InitializeEntity()
    {
      foreach (var bot in _enterable.EnterersInside)
      {
        Deteriorable deteriorable = bot.GetComponent<Deteriorable>();
        if (deteriorable != null) ProtectedBots.TryAdd(deteriorable, true);
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
}
