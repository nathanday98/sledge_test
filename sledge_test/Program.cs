using Raylib_cs;
using Sledge.Formats;
using Sledge.Formats.Map.Formats;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using static Raylib_cs.Raylib;

namespace sledge_test
{
	internal class Program
	{
		static Vector3 from_map(Vector3 v)
		{
			return new Vector3(v.X, v.Z, v.Y);
			//return v;
		}

		struct UVText
		{
			public float u, v;
			public float plane_x, plane_y;
			public Vector2 draw_pos;
		}

		static void Main(string[] args)
		{
			var format = new QuakeMapFormat();
			var map = format.ReadFromFile("C:\\dev\\dngn\\source_data\\test_valve.map");
			var world_spawn = map.Worldspawn;

			InitWindow(1920, 1080, "Hello World");
			SetTargetFPS(60);
			SetExitKey(0);

			Camera3D camera = new Camera3D(new Vector3(10.0f, 196.0f, 10.0f), Vector3.Zero, new Vector3(0.0f, 1.0f, 0.0f), 45.0f, CameraProjection.Perspective);

			int texture_width = 64;
			int texture_height = 64;

			Span<Vector3> dirs = stackalloc Vector3[4];

			Vector2[] projected_bounds = new Vector2[8];

			Span<int> mins = stackalloc int[dirs.Length];
			Span<int> maxs = stackalloc int[dirs.Length];

			List<UVText> uv_texts = new List<UVText>();


			while (!WindowShouldClose())
			{

				uv_texts.Clear();
				if (IsKeyDown(KeyboardKey.F4) && IsKeyDown(KeyboardKey.LeftAlt))
				{
					CloseWindow();
				}

				if (IsCursorHidden())
				{
					UpdateCamera(ref camera, CameraMode.Free);
				}

				// Toggle camera controls
				if (IsMouseButtonPressed(MouseButton.Left) && !IsCursorHidden())
				{
					DisableCursor();
				}

				if (IsKeyPressed(KeyboardKey.Escape) && IsCursorHidden())
				{
					EnableCursor();
				}

				BeginDrawing();
				{
					ClearBackground(Color.White);

					BeginMode3D(camera);
					{
						DrawGrid(10, 1.0f);

						foreach (var child in world_spawn.Children)
						{
							if (child is Sledge.Formats.Map.Objects.Solid solid)
							{
								int face_index = 0;
								foreach (var face in solid.Faces)
								{
									//if (face_index != 0)
									//{
									//	face_index++;
									//	continue;
									//}

									var plane_normal = from_map(face.Plane.Normal);
									var plane_point = from_map(face.Plane.GetPointOnPlane());
									var plane_right = from_map((face.OriginalPlaneVertices[1] - face.OriginalPlaneVertices[0]).Normalise().ToVector3());
									var plane_up = plane_normal.Cross(plane_right).Normalise();

									var plane = Plane.CreateFromVertices(from_map(face.OriginalPlaneVertices[0].ToVector3()), from_map(face.OriginalPlaneVertices[1].ToVector3()), from_map(face.OriginalPlaneVertices[2].ToVector3()));

									//DrawSphere(plane_point, 1.0f, Color.Black);
									//DrawLine3D(plane_point, plane_point + plane_normal * 10.0f, Color.Magenta);

									var average_point = Vector3.Zero;

									for (int i = 0; i < face.Vertices.Count; i++)
									{
										average_point += from_map(face.Vertices[i]);
									}

									average_point /= face.Vertices.Count;

									//DrawSphere(average_point, 1.0f, Color.Black);
									//DrawLine3D(average_point, average_point + plane_normal * 10.0f, Color.Magenta);
									//DrawLine3D(average_point, average_point + plane_right * 10.0f, Color.SkyBlue);
									//DrawLine3D(average_point, average_point + plane_up * 10.0f, Color.Green);

									var u_axis = from_map(face.UAxis);
									var v_axis = from_map(face.VAxis);

									var rotation_axis = plane_normal;

									//var quat = Quaternion.CreateFromAxisAngle(rotation_axis, face.Rotation * MathF.PI / 180);
									var quat = Quaternion.Identity;



									dirs[0] = plane_right;
									dirs[1] = plane_up;
									dirs[2] = -plane_right;
									dirs[3] = -plane_up;

									// I think I can do a lot of this with an x and y coordinate which are the projections along the plane_right and plane_up.
									// I can then effectively work out the rotated bounds in 2d, I would just need to then convert them back to 3d for conversion to proper UVs.

									for (int result_index = 0; result_index < dirs.Length; result_index++)
									{
										float min_proj = float.MaxValue;
										float max_proj = float.MinValue;
										for (int i = 0; i < face.Vertices.Count; i++)
										{
											Vector3 point = Vector3.Transform(from_map(face.Vertices[i]) - plane_point, -quat);
											//DrawSphere(point, 1.0f, Color.Magenta);
											//DrawLine3D(point, average_point, Color.Magenta);
											float proj = point.Dot(dirs[result_index]);
											if (proj < min_proj)
											{
												min_proj = proj;
												mins[result_index] = i;
											}

											if (proj > max_proj)
											{
												max_proj = proj;
												maxs[result_index] = i;
											}
										}
									}

									Vector3 aabb_min = Vector3.Transform(from_map(face.Vertices[mins[0]]) - plane_point, -quat);
									Vector3 aabb_max = Vector3.Transform(from_map(face.Vertices[maxs[0]]) - plane_point, -quat);

									foreach (var point in face.Vertices)
									{
										Vector3 without_plane_rotation = from_map(point) - average_point;
										float plane_space_x = without_plane_rotation.Dot(plane_right);
										float plane_space_y = without_plane_rotation.Dot(plane_up);

										//DrawLine3D(average_point, average_point + plane_right * plane_space_x + plane_up * plane_space_y, Color.Orange);
									}

									for (int result_index = 1; result_index < dirs.Length; result_index++)
									{
										Vector3 min_point = Vector3.Transform(from_map(face.Vertices[mins[result_index]]) - plane_point, -quat);
										Vector3 max_point = Vector3.Transform(from_map(face.Vertices[maxs[result_index]]) - plane_point, -quat);

										if (min_point.X < aabb_min.X)
										{
											aabb_min.X = min_point.X;
										}

										if (min_point.Y < aabb_min.Y)
										{
											aabb_min.Y = min_point.Y;
										}

										if (min_point.Z < aabb_min.Z)
										{
											aabb_min.Z = min_point.Z;
										}

										if (max_point.X > aabb_max.X)
										{
											aabb_max.X = max_point.X;
										}

										if (max_point.Y > aabb_max.Y)
										{
											aabb_max.Y = max_point.Y;
										}

										if (max_point.Z > aabb_max.Z)
										{
											aabb_max.Z = max_point.Z;
										}
									}

									Vector3 texture_offset = (u_axis * (face.XShift % (float)texture_width) + (v_axis * (face.YShift % (float)texture_height)));

									Vector3 aabb_center = plane_point + (aabb_max + aabb_min) * 0.5f;
									Vector3 aabb_size = (aabb_max - aabb_min) /*+ texture_offset*/;

									Raylib_cs.Rlgl.PushMatrix();
									//Raylib_cs.Rlgl.Rotatef(face.Rotation, rotation_axis.X, rotation_axis.Y, rotation_axis.Z);

									if (face_index == 0)
									{
										//DrawCubeWiresV(aabb_center, aabb_size, Color.Magenta);
									}
									Vector3 texture_size = (u_axis * texture_width * face.XScale + v_axis * texture_height * face.YScale);

									Vector3 aabb_div = Vector3.Zero;
									// Make minimum 1 so the loops below have at least one iteration
									Vector3 aabb_div_rounded = Vector3.One;
									for (int i = 0; i < 3; i++)
									{
										float t = texture_size[i];
										if (t == 0.0f)
										{
											continue;
										}

										float num = Math.Abs(aabb_size[i] / texture_size[i]);
										aabb_div[i] = num;
										float rounded_num = MathF.Ceiling(num);
										if (rounded_num % 2 != 0)
										{
											rounded_num += 1;
										}
										aabb_div_rounded[i] = rounded_num;
									}

									Vector3 padded_aabb_size = aabb_div_rounded * texture_size;
									//DrawCubeWiresV(aabb_center, padded_aabb_size, Color.DarkPurple);
									//if (face_index == 4)
									{
										for (int x = 0; x < (int)aabb_div_rounded.X; x++)
										{
											for (int y = 0; y < (int)aabb_div_rounded.Y; y++)
											{
												for (int z = 0; z < (int)aabb_div_rounded.Z; z++)
												{
													Vector3 local_offset = new Vector3(x, y, z) * texture_size;
													Vector3 position = aabb_center - (padded_aabb_size * 0.5f) + local_offset - texture_offset;
													//DrawCubeWiresV(position + texture_size * 0.5f, texture_size, Color.Lime);
												}

											}

										}
									}


									//Raylib_cs.Rlgl.Rotatef(-face.Rotation, rotation_axis.X, rotation_axis.Y, rotation_axis.Z);
									Raylib_cs.Rlgl.PopMatrix();

									//DrawSphere(Vector3.Transform(aabb_min, quat), 1.0f, Color.Magenta);
									//DrawSphere(Vector3.Transform(aabb_max, quat), 1.0f, Color.Magenta);
									//DrawLine3D(Vector3.Transform(aabb_min, quat), Vector3.Transform(aabb_max, quat), Color.Magenta);

									//DrawSphere(Vector3.Transform(aabb_min, quat), 1.0f, Color.Magenta);
									//DrawLine3D(Vector3.Transform(aabb_min, quat), average_point, Color.Magenta);

									//DrawSphere(Vector3.Transform(aabb_max, quat), 1.0f, Color.Magenta);
									//DrawLine3D(Vector3.Transform(aabb_max, quat), average_point, Color.Magenta);


									//u_axis = Vector3.Transform(u_axis, quat);
									//v_axis = Vector3.Transform(v_axis, quat);

									//DrawCapsuleWires(average_point, average_point + u_axis * 5.0f, 1.0f, 10, 10, Color.Red);
									//DrawCapsuleWires(average_point, average_point + v_axis * 5.0f, 1.0f, 10, 10, Color.Green);
									//DrawCapsuleWires(average_point, average_point + rotation_axis * 5.0f, 1.0f, 10, 10, Color.Blue);

									Vector3 texture_space_right = from_map(face.UAxis).Normalise();
									Vector3 texture_space_up = from_map(face.VAxis).Normalise();
									Vector3 texture_space_forward = texture_space_up.Cross(texture_space_right).Normalise();

									Matrix4x4 texture_space_basis = new Matrix4x4(
										texture_space_right.X, texture_space_up.X, texture_space_forward.X, 0,
										texture_space_right.Y, texture_space_up.Y, texture_space_forward.Y, 0,
										texture_space_right.Z, texture_space_up.Z, texture_space_forward.Z, 0,
										0, 0, 0, 1
									);

									Matrix4x4 model_to_texture_space = texture_space_basis * Matrix4x4.CreateScale(1.0f / face.XScale, 1.0f / face.YScale, 1.0f) * Matrix4x4.CreateTranslation(face.XShift, face.YShift, 0.0f);

									Matrix4x4 texture_space_to_model = Matrix4x4.CreateTranslation(-face.XShift, -face.YShift, 0.0f) * Matrix4x4.CreateScale(face.XScale, face.YScale, 1.0f) * Matrix4x4.Transpose(texture_space_basis);


									//Matrix4x4 texture_space_to_model = Matrix4x4.CreateTranslation(-face.XShift, -face.YShift, 0) * Matrix4x4.Transpose(texture_space_basis);
									//Matrix4x4 texture_space_to_model = Matrix4x4.Identity;
									//Debug.Assert(Matrix4x4.Invert(model_to_texture_space, out texture_space_to_model));

									if (face_index == 4)
									{
										Vector3 new_u_axis = from_map(face.UAxis) / face.XScale;
										Vector3 new_v_axis = from_map(face.VAxis) / face.YScale;

										var previous_point = from_map(face.Vertices[0]);
										var start_point = previous_point;

										for (int i = 1; i < face.Vertices.Count; i++)
										{
											var point = from_map(face.Vertices[i]);
											DrawLine3D(previous_point, point, Color.Red);
											previous_point = point;
											float u = point.Dot(new_u_axis);
											float v = point.Dot(new_v_axis);
											u += face.XShift;
											v += face.YShift;
											//u /= (float)texture_width;
											//v /= (float)texture_height;
											//if (face_index == 3)
											{
											}

											var texture_space_point = model_to_texture_space.Transform(point);

											//Debug.Assert(u == texture_space_point.X);
											//Debug.Assert(v == texture_space_point.Y);

											var model_space_point = texture_space_to_model.Transform(texture_space_point);

											//Debug.Assert(model_space_point == point);

										}
										DrawLine3D(previous_point, start_point, Color.Red);


										var texture_space_center = new Vector3(face.XShift, face.YShift, 0.0f);

										DrawSphere(texture_space_center, 1.0f, Color.Black);
										DrawLine3D(texture_space_center, texture_space_center + texture_space_forward * 10.0f, Color.Magenta);
										DrawLine3D(texture_space_center, texture_space_center + texture_space_right * 10.0f, Color.SkyBlue);
										DrawLine3D(texture_space_center, texture_space_center + texture_space_up * 10.0f, Color.Green);


										//DrawSphere(plane.Project(bottom_left_back), 1.0f, Color.DarkGreen);
										//DrawSphere(plane.Project(top_left_back), 1.0f, Color.DarkGreen);
										//DrawSphere(plane.Project(top_right_back), 1.0f, Color.DarkGreen);
										//DrawSphere(plane.Project(bottom_right_back), 1.0f, Color.DarkGreen);

										//DrawSphere(plane.Project(bottom_left_front), 1.0f, Color.DarkGreen);
										//DrawSphere(plane.Project(top_left_front), 1.0f, Color.DarkGreen);
										//DrawSphere(plane.Project(top_right_front), 1.0f, Color.DarkGreen);
										//DrawSphere(plane.Project(bottom_right_front), 1.0f, Color.DarkGreen);

										// I can construct a plane in texture space and then use that to map 2d texture space coordinates (on the plane) to 3d texture space then to model space

										Vector3 texture_plane_vert0 = model_to_texture_space.Transform(from_map(face.Vertices[0]));
										Vector3 texture_plane_vert1 = model_to_texture_space.Transform(from_map(face.Vertices[1]));
										Vector3 texture_plane_vert2 = model_to_texture_space.Transform(from_map(face.Vertices[2]));
										Plane texture_plane = Plane.CreateFromVertices(texture_plane_vert0, texture_plane_vert1, texture_plane_vert2);
										Vector3 texture_plane_right = (texture_plane_vert1 - texture_plane_vert0).Normalise();
										Vector3 texture_plane_up = texture_plane.Normal.Cross(texture_plane_right).Normalise();
										Vector3 texture_plane_point = texture_plane.GetPointOnPlane();

										Vector3 texture_plane_right_in_model_space = Vector3.TransformNormal(texture_plane_right, texture_space_to_model);
										Vector3 texture_plane_up_in_model_space = Vector3.TransformNormal(texture_plane_up, texture_space_to_model);
										Vector3 texture_plane_center_in_model_space = texture_space_to_model.Transform(texture_plane_point);


										//DrawSphere(texture_plane_center_in_model_space, 1.0f, Color.Red);
										//DrawCylinderEx(texture_plane_center_in_model_space, texture_plane_center_in_model_space + Vector3.TransformNormal(texture_plane.Normal, texture_space_to_model) * 10.0f, 1.5f, 1.5f, 12, Color.Magenta);
										//DrawCylinderEx(texture_plane_center_in_model_space, texture_plane_center_in_model_space + Vector3.TransformNormal(texture_plane_right, texture_space_to_model) * texture_width, 1.5f, 1.5f, 12, Color.SkyBlue);
										//DrawCylinderEx(texture_plane_center_in_model_space, texture_plane_center_in_model_space + Vector3.TransformNormal(texture_plane_up, texture_space_to_model) * texture_height, 1.5f, 1.5f, 12, Color.Green);

										Vector2 texture_plane_aabb_min = new Vector2(float.MaxValue, float.MaxValue);
										Vector2 texture_plane_aabb_max = new Vector2(-float.MaxValue, -float.MaxValue);
										foreach (var point_raw in face.Vertices)
										{
											var point = from_map(point_raw);

											var texture_space_point = model_to_texture_space.Transform(point);
											texture_space_point -= texture_plane_point;
											float x = texture_space_point.Dot(texture_plane_right);
											float y = texture_space_point.Dot(texture_plane_up);

											//var model_space_point = texture_space_to_model.Transform(texture_plane_point + texture_plane_right * x + texture_plane_up * y);
											//DrawSphere(model_space_point, 1.0f, Color.Purple);

											//uv_texts.Add(new UVText() { u = texture_space_point.X, v = texture_space_point.Y, draw_pos = GetWorldToScreen(point, camera) });


											if (x < texture_plane_aabb_min.X)
											{
												texture_plane_aabb_min.X = x;
											}

											if (y < texture_plane_aabb_min.Y)
											{
												texture_plane_aabb_min.Y = y;
											}

											if (x > texture_plane_aabb_max.X)
											{
												texture_plane_aabb_max.X = x;
											}

											if (y > texture_plane_aabb_max.Y)
											{
												texture_plane_aabb_max.Y = y;
											}
										}

										texture_plane_aabb_min.X = MathF.Floor(texture_plane_aabb_min.X / texture_width) * texture_width;
										texture_plane_aabb_min.Y = MathF.Floor(texture_plane_aabb_min.Y / texture_height) * texture_height;
										texture_plane_aabb_max.X = MathF.Ceiling(texture_plane_aabb_max.X / texture_width) * texture_width;
										texture_plane_aabb_max.Y = MathF.Ceiling(texture_plane_aabb_max.Y / texture_height) * texture_height;
										var texture_plane_aabb_size = (texture_plane_aabb_max - texture_plane_aabb_min) / new Vector2(texture_width, texture_height);
										var texture_plane_to_texture_space = (float x, float y) => texture_plane_point + texture_plane_right * x + texture_plane_up * y;

										var texture_plane_to_model_space = (float x, float y) =>
										{
											return texture_space_to_model.Transform(texture_plane_point + texture_plane_right * x + texture_plane_up * y);
										};

										Vector3 texture_bottom_left = texture_plane_to_model_space(texture_plane_aabb_min.X, texture_plane_aabb_min.Y);
										Vector3 texture_top_left = texture_plane_to_model_space(texture_plane_aabb_min.X, texture_plane_aabb_max.Y);
										Vector3 texture_top_right = texture_plane_to_model_space(texture_plane_aabb_max.X, texture_plane_aabb_max.Y);
										Vector3 texture_bottom_right = texture_plane_to_model_space(texture_plane_aabb_max.X, texture_plane_aabb_min.Y);

										//DrawLine3D(texture_bottom_left, texture_top_left, Color.Green);
										//DrawLine3D(texture_top_left, texture_top_right, Color.Green);
										//DrawLine3D(texture_top_right, texture_bottom_right, Color.Green);
										//DrawLine3D(texture_bottom_right, texture_bottom_left, Color.Green);

										var draw_plane_space_texture_coords = (float plane_x, float plane_y) =>
										{
											var texture_space = texture_plane_to_texture_space(plane_x, plane_y);
											var model_space = texture_space_to_model.Transform(texture_space);
											uv_texts.Add(new UVText() { u = texture_space.X, v = texture_space.Y, draw_pos = GetWorldToScreen(model_space, camera), plane_x = plane_x, plane_y = plane_y });
										};

										//draw_plane_space_texture_coords(texture_plane_aabb_min.X, texture_plane_aabb_min.Y);
										//draw_plane_space_texture_coords(texture_plane_aabb_min.X, texture_plane_aabb_max.Y);
										//draw_plane_space_texture_coords(texture_plane_aabb_max.X, texture_plane_aabb_max.Y);
										//draw_plane_space_texture_coords(texture_plane_aabb_max.X, texture_plane_aabb_min.Y);

										var texture_space_plane_min = texture_plane_to_texture_space(texture_plane_aabb_min.X, texture_plane_aabb_min.Y);
										var texture_space_plane_max = texture_plane_to_texture_space(texture_plane_aabb_max.X, texture_plane_aabb_max.Y);

										//texture_space_plane_min.X = MathF.Floor(texture_space_plane_min.X / (float)texture_width) * (float)texture_width;
										//texture_space_plane_min.Y = MathF.Floor(texture_space_plane_min.Y / (float)texture_height) * (float)texture_height;

										//texture_space_plane_max.X = MathF.Ceiling(texture_space_plane_max.X / (float)texture_width) * (float)texture_width;
										//texture_space_plane_max.Y = MathF.Ceiling(texture_space_plane_max.Y / (float)texture_height) * (float)texture_height;

										var dist_along_right = texture_space_plane_min.Dot(texture_plane_right);
										dist_along_right = dist_along_right >= 0 ? MathF.Ceiling(dist_along_right) : MathF.Floor(dist_along_right);

										var dist_along_up = texture_space_plane_min.Dot(texture_plane_up);
										dist_along_up = dist_along_up >= 0 ? MathF.Ceiling(dist_along_up) : MathF.Floor(dist_along_up);

										texture_space_plane_min = texture_space_right * dist_along_right + texture_plane_up * dist_along_up;

										//DrawSphere(texture_space_plane_min, 1.0f, Color.Purple);

										for (float x = texture_space_plane_min.X; x < texture_space_plane_max.X; x += (float)texture_width)
										{
											for (float y = texture_space_plane_min.Y; y < texture_space_plane_max.Y; y += (float)texture_height)
											{
												//var texture_space_point =
											}
										}

										DrawLine3D(texture_space_to_model.Transform(texture_space_plane_min), texture_space_to_model.Transform(texture_space_plane_min + texture_plane_right * (float)texture_width), Color.Black);

										

										float texture_right_on_plane_x = texture_space_right.Dot(plane_right);
										float texture_right_on_plane_y = texture_space_right.Dot(plane_up);
										//float texture_right_on_plane_z = texture_space_right.Dot(plane_normal) * texture_width;
										float texture_up_on_plane_x = texture_space_up.Dot(plane_right);
										float texture_up_on_plane_y = texture_space_up.Dot(plane_up);
										//float texture_up_on_plane_z = texture_space_up.Dot(plane_normal) * texture_height;

										Vector3 texture_right_on_plane = (plane_right * texture_right_on_plane_x + plane_up * texture_right_on_plane_y).Normalise();
										Vector3 texture_up_on_plane = (plane_right * texture_up_on_plane_x + plane_up * texture_up_on_plane_y).Normalise();
										//var texture_right_on_plane = texture_space_right;
										//var texture_up_on_plane = texture_space_up;

										Vector3 texture_on_plane_center = texture_plane_center_in_model_space;

										DrawLine3D(texture_on_plane_center, texture_on_plane_center + texture_plane_right_in_model_space * 10, Color.Gold);
										DrawLine3D(texture_on_plane_center, texture_on_plane_center + texture_plane_up_in_model_space * 10, Color.Gold);

										//for (int i = -5; i < 5; i++)
										//{
										//	for (int j = -5; j < 5; j++)
										//	{
										//		for (int k = -5; k < 5; k++)
										//		{
										//			var pos = texture_space_center + texture_space_right * i * texture_width * face.XScale + texture_space_up * j * texture_height * face.YScale + texture_space_forward;
										//			var pos_on_plane = plane.Project(pos);
										//			DrawSphere(pos_on_plane, 1.0f, Color.Gold);
										//		}

										//	}

										//}

										var find_furthest_projection_along = (Vector3 dir) =>
										{
											float max_proj = float.MinValue;
											for (int i = 0; i < face.Vertices.Count; i++)
											{
												Vector3 point = from_map(face.Vertices[i]) - texture_plane_center_in_model_space;
												float proj = point.Dot(dir);
												if (proj > max_proj)
												{
													max_proj = proj;
												}
											}
											return max_proj;
										};

										var furthest = (Vector3 dir) =>
										{
											float proj = find_furthest_projection_along(dir);
											DrawLine3D(texture_plane_center_in_model_space, texture_plane_center_in_model_space + dir * proj, Color.Green);
										};

										for (int i = -5; i < 5; i++)
										{
											var start = texture_on_plane_center + texture_plane_right_in_model_space * i * texture_width + texture_plane_up_in_model_space * -5 * texture_height;
											var end = texture_on_plane_center + texture_plane_right_in_model_space * i * texture_width + texture_plane_up_in_model_space * 5 * texture_height;
											//DrawLine3D(start, end, Color.Gold);
										}

										for (int i = -5; i < 5; i++)
										{
											var start = texture_on_plane_center + texture_plane_right_in_model_space * -5 * texture_width + texture_plane_up_in_model_space * i * texture_height;
											var end = texture_on_plane_center + texture_plane_right_in_model_space * 5 * texture_width + texture_plane_up_in_model_space * i * texture_height;
											//DrawLine3D(start, end, Color.Gold);
										}

										var grid_pos = (int x, int y) =>
										{
											return texture_bottom_left + texture_plane_right_in_model_space * x * texture_width + texture_plane_up_in_model_space * y * texture_height;
										};

										for(int x = 0; x < (int)texture_plane_aabb_size.X; x++)
										{
											for (int y = 0; y < (int)texture_plane_aabb_size.Y; y++)
											{
												var grid_bottom_left = grid_pos(x, y);
												var grid_bottom_right = grid_pos(x+1, y);
												var grid_top_left = grid_pos(x, y+1);
												var grid_top_right = grid_pos(x+1, y+1);
												DrawLine3D(grid_bottom_left, grid_top_left, Color.Brown);
												DrawLine3D(grid_top_left, grid_top_right, Color.Brown);
												DrawLine3D(grid_top_right, grid_bottom_right, Color.Brown);
												DrawLine3D(grid_bottom_right, grid_bottom_left, Color.Brown);
											}
										}


										//furthest(texture_plane_right_in_model_space);
										//furthest(-texture_plane_right_in_model_space);

										//furthest(texture_plane_up_in_model_space);
										//furthest(-texture_plane_up_in_model_space);

										DrawSphere(texture_bottom_left, 1.0f, Color.Brown);
										DrawSphere(texture_bottom_right, 1.0f, Color.Brown);
										DrawSphere(texture_top_left, 1.0f, Color.Brown);
										DrawSphere(texture_top_right, 1.0f, Color.Brown);

										//DrawLine3D(from_map(face.Vertices[0]), from_map(face.Vertices[0]) + texture_space_right * furthest_right, Color.Brown);
										//DrawLine3D(from_map(face.Vertices[0]), from_map(face.Vertices[0]) + -texture_space_right * furthest_left, Color.Brown);
										//DrawLine3D(from_map(face.Vertices[0]), from_map(face.Vertices[0]) + texture_space_up * furthest_up, Color.Brown);
										//DrawLine3D(from_map(face.Vertices[0]), from_map(face.Vertices[0]) + -texture_space_up * furthest_down, Color.Brown);
									}
									face_index++;
								}
							}
						}
					}
					EndMode3D();
					foreach (UVText text in uv_texts)
					{
						DrawText($"{text.u}, {text.v} -- {text.plane_x}, {text.plane_y}", (int)text.draw_pos.X, (int)text.draw_pos.Y, 20, Color.Black);
					}
				}
				EndDrawing();
			}

			CloseWindow();
		}
	}
}
