//TOOL CREATED BY JUAN MARTIN SALICE. 2026

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UnityEditor.SceneManagement;

namespace Editor
{
    public class SceneMaterialAuditorEditor : EditorWindow
    {
        //Exposed variables
        [SerializeField] private VisualTreeAsset _tree;
        [SerializeField] private VisualTreeAsset _itemTree;
        [SerializeField] private VisualTreeAsset _dualItemTree;
        [SerializeField] private VisualTreeAsset _materialResourceTree;

        //Scan filter toggles
        private Toggle _meshRendererToggle;
        private Toggle _skinnedMeshRendererToggle;
        private Toggle _spriteRendererToggle;
        private Toggle _lineRendererToggle;
        private Toggle _trailRendererToggle;
        private Toggle _particleSystemToggle;

        //Total results
        private Label _totalMaterialsLabel;
        private Label _totalShadersLabel;
        private Label _totalRenderersLabel;

        //Results list view
        private DropdownField _filterByShaderDropdown;
        private ListView _foundItemsView;

        //Right pane
        private Label _inspectingMaterialLabel;
        private Label _materialPathLabel;
        private Label _materialUsesLabel;
        private Label _materialQueueLabel;
        private Label _shaderNameLabel;
        private Label _shaderPathLabel;
        private ListView _shaderPropertiesView;
        private ListView _materialResourcesView;
        private ListView _shaderKeywordsView;
        private ListView _shaderPassesView;
        private ListView _renderersView;

        //Scan results
        private List<FoundItems> _allFoundItems = new List<FoundItems>();
        private List<FoundItems> _shaderFilteredItems = new List<FoundItems>();
        private List<Shader> _allShaders = new List<Shader>();
        private Renderer[] _allRenderers;
        private List<Renderer> _filteredRenderers = new List<Renderer>();
        
        //Inspecting variables
        private FoundItems _inspectingItem;
        private int _oldInspectingIndex = -1;
        private String[] _inspectingKeywords;
        private List<String> _inspectingPasses = new List<string>();
        private List<String> _inspectingProperties = new List<string>();
        private List <int> _inspectingResources = new List<int>();
        private List<Renderer> _inspectingRenderers = new List<Renderer>();
        private Renderer _selectedRenderer;
        private String _selectedResourcePath;
        
        private Color _pointerInactive = new Color(0.2f, 0.2f, 0.2f, 1);
        private Color _pointerActive = new Color(0f, 0.9f, 0.4f, 1);

        private static SceneMaterialAuditorEditor window;
        //Const variables
        private const String ALL_SHADERS_FILTER = "All Shaders";

        [MenuItem("JM Salice Tools/SceneMaterialAuditor")]
        public static void ShowEditor() //Opens a new editor window when the menu specified above is clicked 
        {
            if (window == null) window = GetWindow<SceneMaterialAuditorEditor>();
            window.titleContent = new GUIContent("Scene Material Auditor");
            EditorSceneManager.sceneOpened += ResetOnSceneChange;
            PrefabStage.prefabStageOpened += ResetOnPrefabChange;
            PrefabStage.prefabStageClosing += ResetOnPrefabChange;
        }

        private void CreateGUI() //Fills the created window with the specified content
        {
            _tree.CloneTree(rootVisualElement); 
            SetBindings();
        }

        private static void ResetOnSceneChange(UnityEngine.SceneManagement.Scene openedScene, OpenSceneMode openMode) //Resets the tool when the scene changes
        {
            window.ClearSearch();
        }
        private static void ResetOnPrefabChange(PrefabStage openedPrefab) //Resets the tool when a prefab is open or closed
        {
            window.ClearSearch();
        }

        #region Bindings
        private void SetBindings() //Binds all UI toolikt elements to the script
        {
            LeftPaneBindings();
            CenterPaneBindings();
            RightPaneBindings();
        }

        private void LeftPaneBindings()
        {
            //Scan button
            Button scanButton = rootVisualElement.Q<Button>("ScanButton");
            scanButton.RegisterCallback<ClickEvent>(ScanMaterials);
            
            //Filters
            _meshRendererToggle = rootVisualElement.Q<Toggle>("MeshRendererToggle");
            _skinnedMeshRendererToggle = rootVisualElement.Q<Toggle>("SkinnedMeshRendererToggle");
            _spriteRendererToggle = rootVisualElement.Q<Toggle>("SpriteRendererToggle");
            _lineRendererToggle = rootVisualElement.Q<Toggle>("LineRendererToggle");
            _trailRendererToggle = rootVisualElement.Q<Toggle>("TrailRendererToggle");
            _particleSystemToggle = rootVisualElement.Q<Toggle>("ParticleSystemToggle");

            Button allButton = rootVisualElement.Q<Button>("AllFiltersButton");
            allButton.RegisterCallback<ClickEvent, bool>(ToggleAllFilters, true);
            Button noneButton = rootVisualElement.Q<Button>("NoneFiltersButton");
            noneButton.RegisterCallback<ClickEvent, bool>(ToggleAllFilters, false);
            
            //Summary
            _totalMaterialsLabel = rootVisualElement.Q<Label>("TotalMaterials");
            _totalShadersLabel = rootVisualElement.Q<Label>("TotalShaders");
            _totalRenderersLabel = rootVisualElement.Q<Label>("TotalRenderers");
        }

        private void CenterPaneBindings()
        {
            _foundItemsView = rootVisualElement.Q<ListView>("FoundItemsView");
            _filterByShaderDropdown = rootVisualElement.Q<DropdownField>("FilterByShader");
            _filterByShaderDropdown.RegisterValueChangedCallback(evt => FilterResults(evt));
        }

        private void RightPaneBindings()
        {
            //Material Bindings
            _inspectingMaterialLabel = rootVisualElement.Q<Label>("InspectingMaterial");
            _materialPathLabel = rootVisualElement.Q<Label>("MaterialPath");
            _materialUsesLabel = rootVisualElement.Q<Label>("MaterialUses");
            _materialQueueLabel = rootVisualElement.Q<Label>("MaterialQueue");
            _materialResourcesView = rootVisualElement.Q<ListView>("MaterialResourcesView");
            if(_materialResourcesView == null) return;
            _materialResourcesView.itemsSource = _inspectingResources;
            _materialResourcesView.makeItem = () => _materialResourceTree.CloneTree();
            _materialResourcesView.bindItem = (e, i) =>
            {
                Texture tex = GetMaterialTexture(_inspectingResources[i]);

                e.Q<Label>("ResourceName").text = "Resource Name: " + tex.name;
                e.Q<Label>("ResourceProperty").text = "Property: " + _inspectingItem.shader.GetPropertyName(_inspectingResources[i]);
                e.Q<Label>("ResourcePath").text = "Path: " + AssetDatabase.GetAssetPath(tex);
                e.Q<Label>("ResourceSize").text = "Size: " + tex.width + "x" + tex.height;
                e.Q<Image>("ResourceImage").image = tex;
            };
            _materialResourcesView.selectionChanged += UpdateResourceSelection;
            
            //Shaders Bindings
            _shaderNameLabel = rootVisualElement.Q<Label>("ShaderName");
            _shaderPathLabel = rootVisualElement.Q<Label>("ShaderPath");
            _shaderKeywordsView = rootVisualElement.Q<ListView>("ShaderKeywordsView");
            _shaderPassesView = rootVisualElement.Q<ListView>("ShaderPassesView");
            _shaderPropertiesView = rootVisualElement.Q<ListView>("ShaderPropertiesView");
            if(_shaderKeywordsView == null) return;
            _shaderKeywordsView.itemsSource = _inspectingKeywords;
            _shaderKeywordsView.makeItem = () => new Label { style = { fontSize = 12 } };
            _shaderKeywordsView.bindItem = (e, i) => e.Q<Label>().text = _inspectingItem.material.shaderKeywords[i];
            if(_shaderPassesView == null) return;
            _shaderPassesView.itemsSource = _inspectingPasses;
            _shaderPassesView.makeItem = () => new Label { style = { fontSize = 12 } };
            _shaderPassesView.bindItem = (e, i) => e.Q<Label>().text = _inspectingPasses[i];
            if(_shaderPropertiesView == null) return;
            _shaderPropertiesView.itemsSource = _inspectingProperties;
            _shaderPropertiesView.makeItem = () => _dualItemTree.CloneTree();
            _shaderPropertiesView.bindItem = (e, i) =>
            {
                e.Q<Label>("ItemName").text = _inspectingProperties[i];
                e.Q<Label>("ItemType").text = _inspectingItem.shader.GetPropertyType(i).ToString();
            };
            
            //Renderer Bindings
            _renderersView = rootVisualElement.Q<ListView>("RenderersView");
            if(_renderersView == null) return;
            _renderersView.selectionChanged += UpdateRendererSelection;

        }
        #endregion

        #region Scan
        private void ScanMaterials(ClickEvent evt) //Does a scan for all the renderers in the scene and creates a list of the found materials and their data.
        {
            ClearSearch();

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            
            if (stage == null)
            {
                _allRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.InstanceID);
            }
            else
            {
               _allRenderers = stage.FindComponentsOfType<Renderer>();
            }

            FilteredScan();

            for(int i = 0; i < _filteredRenderers.Count; i++)
            {
                Material mat = _filteredRenderers[i].sharedMaterial;
                int checkResult = CheckMaterial(mat);
                if (checkResult >= 0)
                {
                    _allFoundItems[checkResult].uses.Add(_filteredRenderers[i]);
                }
                else
                {
                    FoundItems newItem = new FoundItems();
                    newItem.material = mat;
                    newItem.materialPath = AssetDatabase.GetAssetPath(mat);
                    newItem.shader = mat.shader;
                    newItem.shaderPath = AssetDatabase.GetAssetPath(mat.shader);
                    newItem.uses = new List<Renderer>();
                    newItem.uses.Add(_filteredRenderers[i]);
                    newItem.beingInspected = false;
                    _allFoundItems.Add(newItem);
                }

                if(!_allShaders.Contains(mat.shader))
                {
                    _allShaders.Add(mat.shader);
                }
            }

            DisplayResult();
            DisplaySumary();
            UpdateShaderFilters();
            
            _shaderFilteredItems.AddRange(_allFoundItems);
            if (_foundItemsView != null) _foundItemsView.Rebuild();
        }

        private int CheckMaterial(Material mat) //Checks if the material has already been found. If true returns the index of the item that already contains the material, otherwise returns -1
        {
            for(int i = 0; i < _allFoundItems.Count; i++)
            {
                if (_allFoundItems[i].materialPath == AssetDatabase.GetAssetPath(mat)) return i;
            }
            return -1;
        }

        private void ClearSearch() //Clear all lists in the tool
        {
            _allFoundItems.Clear();
            _shaderFilteredItems.Clear();
            _allShaders.Clear();
            _allRenderers = new Renderer[0];
            _filteredRenderers.Clear();

            _inspectingItem = default(FoundItems);
            _oldInspectingIndex = -1;
            _inspectingPasses.Clear();
            _inspectingProperties.Clear();
            _inspectingResources.Clear();
            _inspectingRenderers.Clear();

            _selectedRenderer = null;
            _selectedResourcePath = String.Empty;
            
            _totalMaterialsLabel.text = "Total Materials: -";
            _totalShadersLabel.text = "Total Shaders: -";
            _totalRenderersLabel.text = "Total Renderers: -";
            _inspectingMaterialLabel.text = "Inspecting Material Name";
            _materialPathLabel.text = "Path: -";
            _materialUsesLabel.text = "Uses: -";
            _materialQueueLabel.text = "Render Queue: -";
            _shaderNameLabel.text = "Shader Name: -";
            _shaderPathLabel.text = "Shader Path: -";

            if(_foundItemsView == null) return;
            _foundItemsView.Rebuild();
            if(_materialResourcesView == null) return;
            _materialResourcesView.Rebuild();
            if(_shaderKeywordsView == null) return;
            _shaderKeywordsView.Rebuild();
            if(_shaderPropertiesView == null) return;
            _shaderPropertiesView.Rebuild();
            if(_shaderPassesView == null) return;
            _shaderPassesView.Rebuild();
            if(_renderersView == null) return;
            _renderersView.Rebuild();
        }
        #endregion
        
        #region Filters
        private void UpdateShaderFilters() //Update the shader filters with the latest scan results
        {
            if(_filterByShaderDropdown == null) return;
            _filterByShaderDropdown.choices.Clear();
            _filterByShaderDropdown.choices.Add(ALL_SHADERS_FILTER);
            foreach(Shader s in _allShaders)
            {
                _filterByShaderDropdown.choices.Add(s.name);
            }
            _filterByShaderDropdown.index = 0;
        }

        private void ToggleAllFilters(ClickEvent evt, bool value)
        {
            _meshRendererToggle.value = value;
            _skinnedMeshRendererToggle.value = value;
            _spriteRendererToggle.value = value;
            _lineRendererToggle.value = value;
            _trailRendererToggle.value = value;
            _particleSystemToggle.value = value;
        }

        private void FilteredScan() //Filters the scan according to the UI toggles
        {
            foreach(Renderer r in _allRenderers)
            {
                if (_meshRendererToggle.value == true && r is MeshRenderer) _filteredRenderers.Add(r);
                if (_skinnedMeshRendererToggle.value == true && r is SkinnedMeshRenderer) _filteredRenderers.Add(r);
                if (_spriteRendererToggle.value == true && r is SpriteRenderer) _filteredRenderers.Add(r);
                if (_lineRendererToggle.value == true && r is LineRenderer) _filteredRenderers.Add(r);
                if (_trailRendererToggle.value == true && r is TrailRenderer) _filteredRenderers.Add(r);
                if (_particleSystemToggle.value == true && r is ParticleSystemRenderer) _filteredRenderers.Add(r);
            }
        }

        private void FilterResults(ChangeEvent<String> evt) //Filter the scan results by shader used
        {
            _shaderFilteredItems.Clear();
            if (evt.newValue == ALL_SHADERS_FILTER) 
            {
                _shaderFilteredItems.AddRange(_allFoundItems);
            }
            else
            {
                foreach(FoundItems i in _allFoundItems)
                {
                    if(i.shader.name == evt.newValue)
                    {
                       _shaderFilteredItems.Add(i);
                    }
                }
            }
            if (_foundItemsView == null) return; 
            _foundItemsView.RefreshItems();
        }

        #endregion

        private void DisplaySumary() //Displays the summary after getting the latest scan results
        {
            if (_totalMaterialsLabel == null || _totalShadersLabel == null || _totalRenderersLabel == null) return;
            
            _totalMaterialsLabel.text = "Total Materials: " + _allFoundItems.Count.ToString();
            _totalShadersLabel.text = "Total Shaders: " + _allShaders.Count.ToString();
            _totalRenderersLabel.text = "Total Renderers: " + _filteredRenderers.Count.ToString();
        }

        private void DisplayResult() //Show the results of the scan on a list
        {
            if(_itemTree == null || _foundItemsView == null || _shaderFilteredItems.Count > 0) return;
            
            _foundItemsView.itemsSource = _shaderFilteredItems;
            _foundItemsView.makeItem = () => _itemTree.CloneTree();
            _foundItemsView.bindItem = (e, i) =>
            {
                e.Q<Label>("ItemName").text = _shaderFilteredItems[i].material.name;
                if(_shaderFilteredItems[i].beingInspected)
                {
                    e.Q<Image>("ToggleIcon").tintColor = _pointerActive;
                }
                else
                {
                    e.Q<Image>("ToggleIcon").tintColor = _pointerInactive;
                }
                Button inspectButton = e.Q<Button>("InspectButton");
                inspectButton.RegisterCallback<ClickEvent, int>(UpdateMaterialInspection, i);
            };
        }

        private void UpdateMaterialInspection(ClickEvent evt, int index) //Updates the right pane when inspecting a new material in the tool
        {
            if(_foundItemsView == null) return;

            //Reset previous inspected pointer
            if(_oldInspectingIndex > -1)
            {    
                _inspectingItem.beingInspected = false;
                _allFoundItems[_oldInspectingIndex] = _inspectingItem;

                int oldIndex = _shaderFilteredItems.FindIndex(i => i.material == _allFoundItems[_oldInspectingIndex].material);
                if(oldIndex > -1) _shaderFilteredItems[oldIndex] = _inspectingItem;
            }

            //Set active the new inspected pointer
            _inspectingItem = _shaderFilteredItems[index];
            _inspectingItem.beingInspected = true;
            _shaderFilteredItems[index] = _inspectingItem;
            int allFoundIndex = _allFoundItems.FindIndex(i => i.material ==_shaderFilteredItems[index].material);
            _allFoundItems[allFoundIndex] = _inspectingItem;
            _oldInspectingIndex = allFoundIndex;
            
            //Update the items
            _foundItemsView.RefreshItems();

            _inspectingMaterialLabel.text = _inspectingItem.material.name;

            _materialResourcesView.ClearSelection();
            _shaderPassesView.ClearSelection();
            _shaderKeywordsView.ClearSelection();
            _shaderPropertiesView.ClearSelection();
            _renderersView.ClearSelection();

            DisplayMaterialInfo();
            DisplayShaderInfo();
            DisplayRenderersInfo();
        }

        private void SelectInProject(ClickEvent evt, String path) //Select any asset in the project window using its path
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        #region Material Tab
        private void DisplayMaterialInfo() //Displays the info corresponding to the material tab
        {
            if(_materialResourceTree == null || _inspectingItem.Equals(default(FoundItems))) return;

            if(_materialPathLabel == null || _materialUsesLabel == null || _materialQueueLabel == null) return;
            _materialPathLabel.text = "Path: " + _inspectingItem.materialPath;
            _materialUsesLabel.text = "Uses: " + _inspectingItem.uses.Count;
            _materialQueueLabel.text = "Render Queue: " + _inspectingItem.material.renderQueue;

            if(_materialResourcesView == null) return;
            _inspectingResources.Clear();
            for(int i = 0; i < _inspectingItem.shader.GetPropertyCount(); i++)
            {
                if(_inspectingItem.shader.GetPropertyType(i) == ShaderPropertyType.Texture)
                {
                    String propName = _inspectingItem.shader.GetPropertyName(i);
                    if (_inspectingItem.material.GetTexture(propName) != null)
                    {     
                        _inspectingResources.Add(i);
                    }
                }
            }

            _materialResourcesView.Rebuild();

            Button selectMaterialButton = rootVisualElement.Q<Button>("SelectMaterialButton");
            if(selectMaterialButton == null) return;
            selectMaterialButton.SetEnabled(true);
            selectMaterialButton.RegisterCallback<ClickEvent, String>(SelectInProject, _inspectingItem.materialPath);

            Button selectResourceButton = rootVisualElement.Q<Button>("SelectFocusedResourceButton");
            if(selectResourceButton == null) return;
            selectResourceButton.SetEnabled(true);
            selectResourceButton.RegisterCallback<ClickEvent>(SelectResourceInProject);
        }

        private Texture GetMaterialTexture(int index) //Get the texture used by the material by its property index
        {
            if (_inspectingItem.Equals(default(FoundItems))) return null;

            String propName = _inspectingItem.shader.GetPropertyName(index);
            return _inspectingItem.material.GetTexture(propName);
        }

        private void UpdateResourceSelection(object obj) //Update witch resource is being selected from the list
        {
            if (_materialResourcesView.selectedItem == null) return;
            Texture tex = GetMaterialTexture((int)_materialResourcesView.selectedItem);
            _selectedResourcePath = AssetDatabase.GetAssetPath(tex);
        }

        private void SelectResourceInProject(ClickEvent evt) //Select the focused resource in the project window 
        {
            SelectInProject(evt, _selectedResourcePath);
        }
        #endregion

        #region Shader Tab
        private void DisplayShaderInfo() //Displays the info corresponding to the shader tab
        {
            if(_dualItemTree == null || _shaderKeywordsView == null || _inspectingItem.Equals(default(FoundItems))) return;

            if(_shaderNameLabel == null || _shaderPathLabel == null) return;
            _shaderNameLabel.text = "Shader Name: " + _inspectingItem.shader.name;
            _shaderPathLabel.text = "Shader Path: " + _inspectingItem.shaderPath;

            _inspectingPasses.Clear();
            for (int i = 0;  i < _inspectingItem.shader.passCount; i++)
            {
                _inspectingPasses.Add(_inspectingItem.material.GetPassName(i));
            }

            _inspectingKeywords = _inspectingItem.material.shaderKeywords;

            _inspectingProperties.Clear();
            for(int i = 0; i < _inspectingItem.shader.GetPropertyCount(); i++)
            {
                _inspectingProperties.Add(_inspectingItem.shader.GetPropertyName(i));
            }
            
            _shaderPassesView.Rebuild();
            _shaderKeywordsView.Rebuild();
            _shaderPropertiesView.Rebuild();
            
            Button selectShaderButton = rootVisualElement.Q<Button>("SelectShaderButton");
            if(selectShaderButton == null) return;
            selectShaderButton.SetEnabled(true);
            selectShaderButton.RegisterCallback<ClickEvent, String>(SelectInProject, _inspectingItem.shaderPath);
        }
        #endregion

        #region Renderer Tab
        private void DisplayRenderersInfo() //Displays the info corresponding to the renderers tab
        {
            if(_dualItemTree == null || _renderersView == null || _inspectingItem.Equals(default(FoundItems))) return;

            if(_renderersView == null) return;
            _inspectingRenderers = _inspectingItem.uses;
            _renderersView.itemsSource = _inspectingRenderers;
            _renderersView.makeItem = () => _dualItemTree.CloneTree();
            _renderersView.bindItem = (e, i) =>
            {
                e.Q<Label>("ItemName").text = _inspectingRenderers[i].gameObject.name;
                e.Q<Label>("ItemType").text = _inspectingRenderers[i].GetType().ToString().Replace("UnityEngine.", "");
            };
            
            Button selectAllRenderersButton = rootVisualElement.Q<Button>("SelectAllRenderers");
            if(selectAllRenderersButton == null) return;
            selectAllRenderersButton.SetEnabled(true);
            selectAllRenderersButton.RegisterCallback<ClickEvent>(SelectAllRenderers);

            Button selectFocusRenderersButton = rootVisualElement.Q<Button>("SelectFocusedButton");
            if(selectFocusRenderersButton == null) return;
            selectFocusRenderersButton.SetEnabled(true);
            selectFocusRenderersButton.RegisterCallback<ClickEvent>(SelectSingleRenderer);
        }
        
        private void SelectAllRenderers(ClickEvent evt) //Selects all the renderers of the inspected material
        {
            if(_inspectingItem.Equals(default(FoundItems))) return;
            List<GameObject> gameObjects = new List<GameObject>();
            foreach(Renderer r in _inspectingItem.uses)
            {
                gameObjects.Add(r.gameObject);
            }

            Selection.objects = gameObjects.ToArray();
        }

        private void UpdateRendererSelection(object obj) //Update witch renderer is being selected from the list
        {
            _selectedRenderer = (Renderer)_renderersView.selectedItem;
        }

        private void SelectSingleRenderer(ClickEvent evt) //Selects in the scene only the renderer selected from the list
        {
            if(_selectedRenderer == null) return;

            Selection.activeObject = _selectedRenderer.gameObject;
        }
        #endregion

    }

    [Serializable]
    public struct FoundItems //Struct made to hold the information of each item
    {
        public Material material;
        public String materialPath;
        public Shader shader;
        public String shaderPath;
        public List<Renderer> uses;
        public bool beingInspected;
    }
}
