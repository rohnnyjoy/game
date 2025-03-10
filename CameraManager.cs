using Godot;
using System;

public partial class CameraManager : Node
{
    public static CameraManager Instance { get; private set; }
    
    // Reference to the player's camera
    private Camera3D playerCamera;
    
    // A reference to the current view camera (for rendering)
    private Camera3D currentViewCamera;
    
    public override void _Ready()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            QueueFree();
            return;
        }
    }
    
    public void Initialize(Camera3D playerCam)
    {
        playerCamera = playerCam;
        currentViewCamera = playerCam;
        
        // Ensure the player camera is set as current
        playerCamera.Current = true;
    }
    
    // This method allows any script to get the player's camera
    // for rendering calculations, without changing the current camera
    public Camera3D GetPlayerCamera()
    {
        return playerCamera;
    }
    
    // This ensures the player camera remains the current camera
    public void EnsurePlayerCameraIsCurrent()
    {
        if (playerCamera != null && !playerCamera.Current)
        {
            playerCamera.Current = true;
        }
    }
}