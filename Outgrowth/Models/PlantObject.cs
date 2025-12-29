using Outgrowth.Services;

namespace Outgrowth.Models;

/// <summary>
/// Interactive plant object instance. References a Plant from PlantLibrary for growth stage data
/// </summary>
public class PlantObject : EnvObject, IInteractable
{
    /// <summary>
    /// ID of the plant type from PlantLibrary
    /// </summary>
    public string PlantId { get; set; }
    
    /// <summary>
    /// Reference to the PlantData definition from PlantLibrary
    /// </summary>
    public PlantData? PlantDefinition { get; set; }
    
    /// <summary>
    /// Current growth stage index (0 = seed, max = fully grown)
    /// </summary>
    public int CurrentStage { get; set; }
    
    /// <summary>
    /// Cycle number when the plant was planted (uses current cycle, not a new cycle)
    /// </summary>
    public int PlantedAtCycle { get; set; }
    
    /// <summary>
    /// Total number of cycles the plant has lived since planting
    /// </summary>
    public int CyclesLived { get; set; }
    
    public event EventHandler<TappedEventArgs>? Clicked;
    public event EventHandler? StageChanged;
    public Action? InteractAction { get; set; }
    public bool CanInteract { get; set; } = true;
    
    public PlantObject(string id, string plantId, int x, int y)
        : base(id, x, y, 320, 320, "")
    {
        PlantId = plantId;
        CurrentStage = 0;
        
        // Load plant definition from library
        PlantDefinition = PlantLibrary.GetPlant(plantId);
        
        if (PlantDefinition == null)
        {
            throw new ArgumentException($"Plant with ID '{plantId}' not found in PlantLibrary.", nameof(plantId));
        }
        
        // Determine size based on PlantSize
        double size = GetSizeForPlantSize(PlantDefinition.PlantSize);
        Width = size;
        Height = size;
        
        // Set initial sprite
        BaseSprite = PlantDefinition.GetSpriteForStage(0);
        
        // Record planting cycle (uses current cycle, not a new cycle)
        PlantedAtCycle = PlantsManager.Instance.CurrentCycle;
        CyclesLived = 0; // Start with 0 cycles lived
        System.Diagnostics.Debug.WriteLine($"[PlantObject] Created plant {Id} (PlantId: {PlantId}), PlantedAtCycle: {PlantedAtCycle}, CyclesLived: {CyclesLived}");
        
        // Register plant with PlantsManager for growth updates
        PlantsManager.Instance.RegisterPlant(this);
    }
    
    /// <summary>
    /// Creates a PlantObject from saved data. Used when loading from save file.
    /// CurrentStage will be calculated automatically based on CyclesLived via UpdateGrowth().
    /// ID is generated automatically as "plant_{plantId}_{potNumber}".
    /// </summary>
    public static PlantObject FromSaveData(string plantId, int potNumber, int x, int y, 
        int plantedAtCycle, int cyclesLived)
    {
        // Generate ID from plantId and potNumber
        string id = $"plant_{plantId}_{potNumber}";
        
        var plant = new PlantObject(id, plantId, x, y)
        {
            PlantedAtCycle = plantedAtCycle,
            CyclesLived = cyclesLived
        };
        
        // CurrentStage will be calculated by UpdateGrowth() based on CyclesLived
        // For now, set initial sprite to stage 0 (will be updated by UpdateGrowth())
        if (plant.PlantDefinition != null)
        {
            plant.BaseSprite = plant.PlantDefinition.GetSpriteForStage(0);
        }
        
        System.Diagnostics.Debug.WriteLine($"[PlantObject] Loaded from save: {id}, plantedAtCycle: {plantedAtCycle}, cyclesLived: {cyclesLived}");
        
        return plant;
    }
    
    /// <summary>
    /// Gets the size (width and height) for a given plant size category
    /// </summary>
    private static double GetSizeForPlantSize(string plantSize)
    {
        return plantSize switch
        {
            "Small" => 320,
            "Medium" => 480, // Future: Medium plants
            "Large" => 640,  // Future: Large plants
            _ => 320 // Default to Small
        };
    }
    
    /// <summary>
    /// Gets the current sprite for the current growth stage
    /// </summary>
    public string GetCurrentSprite()
    {
        if (PlantDefinition == null)
        {
            return "";
        }
        return PlantDefinition.GetSpriteForStage(CurrentStage);
    }
    
    /// <summary>
    /// Gets the total number of growth stages
    /// </summary>
    public int TotalStages => PlantDefinition?.TotalStages ?? 0;
    
    /// <summary>
    /// Checks if the plant is fully grown
    /// </summary>
    public bool IsFullyGrown => PlantDefinition != null && CurrentStage >= PlantDefinition.TotalStages - 1;
    
    /// <summary>
    /// Event handler for growth updates from PlantsManager
    /// </summary>
    public void OnGrowthUpdate(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[PlantObject] OnGrowthUpdate called for plant: {Id} (PlantId: {PlantId})");
        UpdateGrowth();
    }
    
    /// <summary>
    /// Updates plant growth based on elapsed cycles. Called automatically via PlantsManager events.
    /// </summary>
    private void UpdateGrowth()
    {
        System.Diagnostics.Debug.WriteLine($"[PlantObject] UpdateGrowth called for plant: {Id}, CurrentStage: {CurrentStage}");
        
        if (PlantDefinition == null)
        {
            System.Diagnostics.Debug.WriteLine($"[PlantObject] PlantDefinition is null for plant: {Id}");
            return;
        }
        
        if (IsFullyGrown)
        {
            System.Diagnostics.Debug.WriteLine($"[PlantObject] Plant {Id} is fully grown, skipping update");
            return;
        }
        
        int currentCycle = PlantsManager.Instance.CurrentCycle;
        CyclesLived = currentCycle - PlantedAtCycle;
        
        System.Diagnostics.Debug.WriteLine($"[PlantObject] Plant {Id}: currentCycle={currentCycle}, PlantedAtCycle={PlantedAtCycle}, CyclesLived={CyclesLived}, CurrentStage={CurrentStage}");
        
        // Calculate which stage the plant should be at based on total cycles lived
        // Sum up cycles needed for each stage transition
        int accumulatedCycles = 0;
        int targetStage = 0;
        
        for (int i = 0; i < PlantDefinition.GrowthStageCycles.Length; i++)
        {
            int cyclesNeeded = PlantDefinition.GrowthStageCycles[i];
            
            if (cyclesNeeded <= 0)
            {
                // Last stage (0 cycles needed means fully grown)
                targetStage = i;
                break;
            }
            
            accumulatedCycles += cyclesNeeded;
            
            if (CyclesLived >= accumulatedCycles)
            {
                // Can reach this stage
                targetStage = i + 1;
            }
            else
            {
                // Not enough cycles for this stage
                break;
            }
        }
        
        // Limit to max stage
        if (targetStage >= PlantDefinition.TotalStages)
        {
            targetStage = PlantDefinition.TotalStages - 1;
        }
        
        // If we advanced stages, update the plant
        if (targetStage > CurrentStage)
        {
            System.Diagnostics.Debug.WriteLine($"[PlantObject] Plant {Id} advancing from stage {CurrentStage} to {targetStage} (CyclesLived: {CyclesLived})");
            ChangeGrowthStage(targetStage);
        }
        else
        {
            int cyclesNeededForNext = CurrentStage < PlantDefinition.GrowthStageCycles.Length 
                ? PlantDefinition.GrowthStageCycles[CurrentStage] 
                : 0;
            System.Diagnostics.Debug.WriteLine($"[PlantObject] Plant {Id} staying at stage {CurrentStage} (needs {cyclesNeededForNext} cycles for next stage, has lived {CyclesLived} cycles)");
        }
    }
    
    /// <summary>
    /// Changes the growth stage to the specified stage and updates the sprite
    /// </summary>
    public void ChangeGrowthStage(int newStage)
    {
        System.Diagnostics.Debug.WriteLine($"[PlantObject] ChangeGrowthStage called for plant {Id}: {CurrentStage} -> {newStage}");
        
        if (PlantDefinition == null)
        {
            System.Diagnostics.Debug.WriteLine($"[PlantObject] PlantDefinition is null, cannot change stage");
            return;
        }
        
        // Validate stage range
        if (newStage < 0 || newStage >= PlantDefinition.TotalStages)
        {
            System.Diagnostics.Debug.WriteLine($"[PlantObject] Invalid stage {newStage} (range: 0-{PlantDefinition.TotalStages - 1})");
            return;
        }
        
        CurrentStage = newStage;
        
        // Update sprite
        BaseSprite = PlantDefinition.GetSpriteForStage(CurrentStage);
        System.Diagnostics.Debug.WriteLine($"[PlantObject] Plant {Id} stage changed to {CurrentStage}, CyclesLived: {CyclesLived}, sprite: {BaseSprite}");
        
        // Notify that stage changed (for saving)
        StageChanged?.Invoke(this, EventArgs.Empty);
        
        // Update visual element if it exists
        if (VisualElement != null)
        {
            System.Diagnostics.Debug.WriteLine($"[PlantObject] Updating visual sprite for plant {Id}");
            UpdatePlantSprite();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[PlantObject] VisualElement is null for plant {Id}, cannot update sprite");
        }
    }
    
    /// <summary>
    /// Updates the plant sprite in the visual element
    /// </summary>
    private void UpdatePlantSprite()
    {
        if (VisualElement is Grid mainGrid && mainGrid.Children.Count > 0)
        {
            if (mainGrid.Children[0] is Border plantBorder && plantBorder.Content != null)
            {
                var spritePath = GetCurrentSprite();
                
                // Update sprite view based on type
                if (!string.IsNullOrEmpty(spritePath) && 
                    spritePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    // Update Image source
                    if (plantBorder.Content is Image plantImage)
                    {
                        plantImage.Source = spritePath;
                    }
                    else
                    {
                        // Replace Label with Image
                        plantBorder.Content = new Image
                        {
                            Source = spritePath,
                            Aspect = Aspect.Fill,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill
                        };
                    }
                }
                else
                {
                    // Update Label text
                    if (plantBorder.Content is Label plantLabel)
                    {
                        plantLabel.Text = spritePath;
                    }
                    else
                    {
                        // Replace Image with Label
                        plantBorder.Content = new Label
                        {
                            Text = spritePath,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center,
                            FontSize = Width * 0.6,
                            TextColor = Colors.White
                        };
                    }
                }
            }
        }
    }
    
    public void OnInteract()
    {
        if (!CanInteract) return;
        InteractAction?.Invoke();
    }
    
    /// <summary>
    /// Harvests the plant (removes it and collects resources)
    /// </summary>
    public void Harvest()
    {
        System.Diagnostics.Debug.WriteLine($"[PlantObject] Harvesting plant {Id} (PlantId: {PlantId})");
        
        // Unregister from PlantsManager
        PlantsManager.Instance.UnregisterPlant(this);
        
        // TODO: Collect resources from the plant based on its stage and type
        // For now, just destroy the plant
        
        System.Diagnostics.Debug.WriteLine($"[PlantObject] Plant {Id} harvested and removed");
    }
    
    public override View CreateVisualElement()
    {
        var mainGrid = new Grid
        {
            WidthRequest = Width,
            HeightRequest = Height
        };
        
        var plantBorder = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Colors.Transparent,
            HeightRequest = Height,
            WidthRequest = Width
        };
        
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (sender, e) =>
        {
            // Pass this PlantObject as sender, not the visual element
            Clicked?.Invoke(this, e);
            OnInteract();
        };
        plantBorder.GestureRecognizers.Add(tapGesture);
        
        // Plant sprite (emoji or image)
        var spritePath = GetCurrentSprite();
        View spriteView;
        
        // Check if sprite is an image file (ends with image extension)
        if (!string.IsNullOrEmpty(spritePath) && 
            (spritePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
        {
            // Use Image for PNG files - fill entire container
            spriteView = new Image
            {
                Source = spritePath,
                Aspect = Aspect.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
        }
        else
        {
            // Use Label for emoji or text
            spriteView = new Label
            {
                Text = spritePath,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                FontSize = Width * 0.6, // Sprite size proportional to width
                TextColor = Colors.White
            };
        }
        
        // Update sprite when CurrentStage changes (for future growth logic)
        // This will be handled when growth logic is implemented
        
        plantBorder.Content = spriteView;
        mainGrid.Children.Add(plantBorder);
        
        VisualElement = mainGrid;
        return mainGrid;
    }
}

