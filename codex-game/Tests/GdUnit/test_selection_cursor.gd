extends GdUnitTestSuite


func test_sync_to_camera_look_updates_target_tile() -> void:
	var cursor = preload("res://Infrastructure/Scenes/SelectionCursor.cs").new()
	cursor.MapSize = Vector2i(8, 8)
	cursor.TileSize = 2.0

	var cam := Camera3D.new()
	cam.GlobalPosition = Vector3(0, 10, 0)
	cam.LookAt(Vector3(0, 0, 0), Vector3.UP)
	cursor.Camera = cam

	cursor.SyncToCameraLook()

	assert_vector(cursor.GetSelectedTile(), false).is_equal(Vector3(0, 0, 0))
