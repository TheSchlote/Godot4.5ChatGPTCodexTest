extends GdUnitTestSuite


func test_cell_to_world_uses_elevation() -> void:
	var pathfinder = preload("res://Infrastructure/Pathfinding/AstarPathfinding.cs").new()
	pathfinder.SetupGrid(4, 4, 2.0, 0.5)
	pathfinder.SetElevation(Vector3i(1, 0, 1), 3)

	var world = pathfinder.CellToWorld(Vector3i(1, 0, 1))

	assert_float(world.y).is_equal(1.5)


func test_blocked_cell_breaks_path() -> void:
	var pathfinder = preload("res://Infrastructure/Pathfinding/AstarPathfinding.cs").new()
	pathfinder.SetupGrid(3, 3, 2.0, 0.2)
	pathfinder.BlockCell(Vector3i(1, 0, 0))

	var path = pathfinder.GetPath(Vector3(0, 0, 0), Vector3(4, 0, 0))

	assert_int(path.size()).is_equal(0)
