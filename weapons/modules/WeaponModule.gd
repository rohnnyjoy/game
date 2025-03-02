
extends Node
class_name WeaponModule

# Melee will take trail from bullet and apply it differently
func modify_bullet(bullet: Bullet) ->  Bullet:
	return bullet

func modify_weapon(config: WeaponConfig) -> WeaponConfig:
	return config
