using System.Runtime.InteropServices;
using Clipper2Lib;
using Sledge.Formats;
using Sledge.Formats.Map.Formats;
using System.Diagnostics;
using System.Numerics;

namespace sledge_map_loader
{
	public class Exports
	{
		static Vector3 from_map(Vector3 v)
		{
			return new Vector3(v.X, v.Z, v.Y);
			//return v;
		}

		[UnmanagedCallersOnly(EntryPoint = "load_map")]
		public static int load_map()
		{
			var format = new QuakeMapFormat();
			var map = format.ReadFromFile("C:\\dev\\dngn\\source_data\\test_valve.map");
			var world_spawn = map.Worldspawn;

			int texture_width = 64;
			int texture_height = 64;

			Span<Vector3> dirs = stackalloc Vector3[4];

			Vector2[] projected_bounds = new Vector2[8];

			Span<int> mins = stackalloc int[dirs.Length];
			Span<int> maxs = stackalloc int[dirs.Length];

			int plane_count = 0;

			foreach (var child in world_spawn.Children)
			{
				if (child is Sledge.Formats.Map.Objects.Solid solid)
				{
					int face_index = 0;
					foreach (var face in solid.Faces)
					{
						plane_count++;
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


						var average_point = Vector3.Zero;

						for (int i = 0; i < face.Vertices.Count; i++)
						{
							average_point += from_map(face.Vertices[i]);
						}

						average_point /= face.Vertices.Count;



						Vector3 texture_space_right = from_map(face.UAxis);
						Vector3 texture_space_up = from_map(face.VAxis);
						Vector3 texture_space_forward = texture_space_up.Cross(texture_space_right);

						Matrix4x4 texture_space_basis = new Matrix4x4(
							texture_space_right.X, texture_space_up.X, texture_space_forward.X, 0,
							texture_space_right.Y, texture_space_up.Y, texture_space_forward.Y, 0,
							texture_space_right.Z, texture_space_up.Z, texture_space_forward.Z, 0,
							0, 0, 0, 1
						);

						Matrix4x4 model_to_texture_space = texture_space_basis * Matrix4x4.CreateScale(1.0f / face.XScale, 1.0f / face.YScale, 1.0f) * Matrix4x4.CreateTranslation(face.XShift, face.YShift, 0.0f);

						//Matrix4x4 texture_space_to_model = Matrix4x4.CreateTranslation(-face.XShift, -face.YShift, 0.0f) * Matrix4x4.CreateScale(face.XScale, face.YScale, 1.0f) * Matrix4x4.Transpose(texture_space_basis);


						//Matrix4x4 texture_space_to_model = Matrix4x4.CreateTranslation(-face.XShift, -face.YShift, 0) * Matrix4x4.Transpose(texture_space_basis);
						Matrix4x4 texture_space_to_model = Matrix4x4.Identity;
						Debug.Assert(Matrix4x4.Invert(model_to_texture_space, out texture_space_to_model));

						//if (face_index == 0)
						{
							Vector3 new_u_axis = from_map(face.UAxis) / face.XScale;
							Vector3 new_v_axis = from_map(face.VAxis) / face.YScale;
							{
								var previous_point = from_map(face.Vertices[0]);
								var start_point = previous_point;

								for (int i = 1; i < face.Vertices.Count; i++)
								{
									var point = from_map(face.Vertices[i]);
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
							}

							var texture_space_center = new Vector3(face.XShift, face.YShift, 0.0f);


							// I can construct a plane in texture space and then use that to map 2d texture space coordinates (on the plane) to 3d texture space then to model space

							Vector3 texture_plane_vert0 = model_to_texture_space.Transform(from_map(face.Vertices[0]));
							Vector3 texture_plane_vert1 = model_to_texture_space.Transform(from_map(face.Vertices[1]));
							Vector3 texture_plane_vert2 = model_to_texture_space.Transform(from_map(face.Vertices[2]));

							float dot_with_plane_normal = (texture_plane_vert1 - texture_plane_vert0).Cross(texture_plane_vert2 - texture_plane_vert0).Y;
							if (dot_with_plane_normal < 0)
							{
								texture_plane_vert0 = model_to_texture_space.Transform(from_map(face.Vertices[0]));
								texture_plane_vert1 = model_to_texture_space.Transform(from_map(face.Vertices[2]));
								texture_plane_vert2 = model_to_texture_space.Transform(from_map(face.Vertices[1]));
							}

							Plane texture_plane = Plane.CreateFromVertices(texture_plane_vert0, texture_plane_vert1, texture_plane_vert2);
							Vector3 texture_plane_right = (texture_plane_vert1 - texture_plane_vert0).Normalise();
							Vector3 texture_plane_up = texture_plane.Normal.Cross(texture_plane_right).Normalise();
							Vector3 texture_plane_point = texture_plane.GetPointOnPlane();
							Vector3 texture_plane_avg = model_to_texture_space.Transform(average_point);


							Vector3 texture_plane_right_in_model_space = Vector3.TransformNormal(texture_plane_right, texture_space_to_model);
							Vector3 texture_plane_up_in_model_space = Vector3.TransformNormal(texture_plane_up, texture_space_to_model);
							Vector3 texture_plane_center_in_model_space = texture_space_to_model.Transform(texture_plane_point);
							Vector3 texture_plane_normal_in_model_space = Vector3.TransformNormal(texture_plane.Normal, texture_space_to_model);

							PathsD face_paths = new PathsD();
							double[] face_path_points = new double[face.Vertices.Count * 2];

							var model_space_to_texture_plane_space = (Vector3 point) =>
							{
								var texture_space_point = model_to_texture_space.Transform(point);
								texture_space_point -= texture_plane_point;
								float x = texture_space_point.Dot(texture_plane_right);
								float y = texture_space_point.Dot(texture_plane_up);

								return new Vector2(x, y);
							};

							Vector2 texture_plane_aabb_min = new Vector2(float.MaxValue, float.MaxValue);
							Vector2 texture_plane_aabb_max = new Vector2(-float.MaxValue, -float.MaxValue);
							for (int i = 0; i < face.Vertices.Count; i++)
							{
								Vector2 point = model_space_to_texture_plane_space(from_map(face.Vertices[i]));

								//var model_space_point = texture_space_to_model.Transform(texture_plane_point + texture_plane_right * x + texture_plane_up * y);
								//DrawSphere(model_space_point, 1.0f, Color.Purple);

								Vector3 texture_space_point = model_to_texture_space.Transform(from_map(face.Vertices[i]));

								//uv_texts.Add(new UVText() { u = texture_space_point.X, v = texture_space_point.Y, draw_pos = GetWorldToScreen(from_map(face.Vertices[i]), camera) });

								if (point.X < texture_plane_aabb_min.X)
								{
									texture_plane_aabb_min.X = point.X;
								}

								if (point.Y < texture_plane_aabb_min.Y)
								{
									texture_plane_aabb_min.Y = point.Y;
								}

								if (point.X > texture_plane_aabb_max.X)
								{
									texture_plane_aabb_max.X = point.X;
								}

								if (point.Y > texture_plane_aabb_max.Y)
								{
									texture_plane_aabb_max.Y = point.Y;
								}

								face_path_points[i * 2 + 0] = point.X;
								face_path_points[i * 2 + 1] = point.Y;
							}

							face_paths.Add(Clipper.MakePath(face_path_points));


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


							Vector3 texture_on_plane_center = texture_plane_center_in_model_space;

							var grid_pos = (int x, int y) =>
							{
								return model_space_to_texture_plane_space(texture_bottom_left + texture_plane_right_in_model_space * x * texture_width + texture_plane_up_in_model_space * y * texture_height);
							};

							PathsD quad_paths = new PathsD();

							for (int x = 0; x < (int)texture_plane_aabb_size.X; x++)
							{
								for (int y = 0; y < (int)texture_plane_aabb_size.Y; y++)
								{
									quad_paths.Clear();
									var grid_bottom_left = grid_pos(x, y);
									var grid_bottom_right = grid_pos(x + 1, y);
									var grid_top_left = grid_pos(x, y + 1);
									var grid_top_right = grid_pos(x + 1, y + 1);
									quad_paths.Add(Clipper.MakePath(new double[] { grid_bottom_left.X, grid_bottom_left.Y, grid_top_left.X, grid_top_left.Y, grid_top_right.X, grid_top_right.Y, grid_bottom_right.X, grid_bottom_right.Y }));
									PathsD solution = Clipper.Intersect(quad_paths, face_paths, FillRule.NonZero);
									foreach (PathD path in solution)
									{
										if (path.Count == 0) continue;
										var previous_point = texture_plane_to_model_space((float)path[0].x, (float)path[0].y);
										var start_point = previous_point;
										for (int i = 1; i < path.Count; i++)
										{
											var point = texture_plane_to_model_space((float)path[i].x, (float)path[i].y);
											previous_point = point;
										}
									}
								}
							}
						}
						face_index++;
					}
				}
			}
			return plane_count;
		}

	}

}
