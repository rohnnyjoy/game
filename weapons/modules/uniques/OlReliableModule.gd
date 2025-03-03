extends UniqueModule
class_name OlReliableModule

func modify_bullet(bullet: Bullet) -> Bullet:
	bullet.gravity = 0
	bullet.radius = 5
	return bullet

func modify_weapon(config: WeaponConfig) -> WeaponConfig:
	config.fire_rate = 0.1
	config.ammo = 400
	config.reload_speed = 1
	config.damage = 2
	config.bullet_speed = 10
	config.accuracy = 1
	return config
