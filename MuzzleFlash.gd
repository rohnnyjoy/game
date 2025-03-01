extends GPUParticles3D

@export var flash_duration: float = 0.1
@export var flash_intensity: float = 1.0
@export var flash_color: Color = Color(1, 1, 1)

func trigger_flash() -> void:
    # Adjust properties based on the configuration.
    # For example, you might adjust the lifetime or emission energy.
    lifetime = flash_duration
    process_material.emission_energy = flash_intensity
    process_material.emission_color = flash_color
    
    restart() # Restart the particle system.
    emitting = true
