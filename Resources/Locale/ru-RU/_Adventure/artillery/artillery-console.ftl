artillery-console-title = Монитор наведения блюспейс артиллерии
artillery-console-beacons = Цели
artillery-console-fire = ОГОНЬ!
artillery-console-fire-controls = Система управления огнём
artillery-console-ready = Орудие готово к стрельбе
artillery-console-cooldown = Перезарядка: { $time }
artillery-console-no-cannon = Пушка не найдена
artillery-console-no-beacons = Маяки не найдены
artillery-console-warning = Рекомендуем не стоять напротив ствола в момент выстрела.
artillery-console-toggle = СТАТУС
artillery-console-toggle-on = СТАТУС: ЗАПУЩЕНА
artillery-console-toggle-off = СТАТУС: ОТКЛЮЧЕНА

artillery-fire-announcement = ВНИМАНИЕ, ОБНАРУЖЕН ВЫСТРЕЛ БЛЮСПЕЙС АРТИЛЛЕРИИ! ПРИГОТОВЬТЕСЬ К УДАРУ!
artillery-announcement-sender = Центральное Командование
# Construction stages
ent-BaseBluespaceArtilleryFrameEast = artillery construction frame
ent-BaseBluespaceArtilleryFrameWest = { ent-BaseBluespaceArtilleryFrameEast }
ent-ArtilleryConsoleFrame = каркас консоли артиллерии

# Recipes
construction-recipe-bluespace-artillery = блюспейс артиллерия
construction-recipe-bluespace-artillery-west = блюспейс артиллерия (зеркало)
construction-recipe-bluespace-artillery-desc = Высокотехнологичная артиллерия что использует новые технологии Блюспейс. Грозное оружие.
construction-recipe-bluespace-artillery-west-desc = Высокотехнологичная артиллерия что использует новые технологии Блюспейс. Грозное оружие, направленное на запад.
construction-recipe-bluespace-artillery-console = консоль блюспейс артиллерии
construction-recipe-bluespace-artillery-console-desc = Консоль для управления огнем из Блюспейс артиллерии.

# Circuit board
ent-ArtilleryComputerCircuitboard = консоль артиллерии (консольная плата)
    .desc = Консольная плата для навигационной системой Блюспейс артиллерии.
construction-graph-component-artillery-console-circuit-board = плату консоли артиллерии

# Artillery
ent-BluespaceArtillery = блюспейс артиллерия
    .desc = Высокотехнологичная артиллерия что использует новые технологии Блюспейс! НЕ ТРОГАЙТЕ ИЛИ ВЫ БУДЕТЕ РАСЩЕПЛЕНЫ.
ent-BluespaceArtilleryMirror = { ent-BluespaceArtillery }
    .desc = { ent-BluespaceArtillery.desc }

# Console
ent-ArtilleryConsole = консоль управления артиллерией
    .desc = Консоль управления огнем артиллерийским орудием.

# Explosion
ent-BSAExplosion = прилет от Блюспейс артиллерии

bluespace-artillery-permission-denied = Отказано в доступе!
