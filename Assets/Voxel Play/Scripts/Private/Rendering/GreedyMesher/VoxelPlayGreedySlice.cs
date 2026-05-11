using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

	public class Quad {
		public byte x, y, w, h;
	}

	public class VoxelPlayGreedySlice {

		const byte USED = 255;

        readonly Quad [] qq;
		Quad lastQ;
		int qqCount;

		public VoxelPlayGreedySlice () {
			qq = new Quad[VoxelPlayEnvironment.ONE_Y_ROW];
			for (int k = 0; k < qq.Length; k++) {
				qq [k] = new Quad ();
			}
		}

		public void Clear () {
			qqCount = 0;
		}


		public void AddQuad (int x, int y) {
			if (qqCount > 0 && lastQ.y == y && lastQ.x + lastQ.w == x) {
				lastQ.w++;
			} else {
				Quad q = lastQ = qq [qqCount++];
				q.x = (byte)x;
				q.y = (byte)y;
				q.w = 1;
				q.h = 1;
			}
		}

		public void FlushTriangles (FaceDirection direction, int slice, List<Vector3> vertices, List<int>indices) {
			if (qqCount == 0) {
				return;
			}
			Vector3 pos;
			int index = vertices.Count;
			for (int k = 0; k < qqCount; k++) {
				Quad q1 = qq [k];
				if (q1.x == USED) {
					continue;
				}
				for (int j = k + 1; j < qqCount; j++) {
					Quad q2 = qq [j];
					if (q2.x == USED)
						continue;
                    if (q1.y == q2.y && q1.h == q2.h && q1.x + q1.w == q2.x) {
                        q1.w += q2.w;
                        q2.x = USED;
                        continue;
                    }
					if (q1.x == q2.x && q1.w == q2.w && q1.y + q1.h == q2.y) {
						q1.h += q2.h;
						q2.x = USED;
						continue;
					}
				}

				float w = q1.w + VoxelPlayGreedyCommon.PADDING2;
				float h = q1.h + VoxelPlayGreedyCommon.PADDING2;

				switch (direction) {
				case FaceDirection.Top:
					pos.y = slice - (VoxelPlayEnvironment.CHUNK_HALF_SIZE-1);
					pos.x = q1.x - VoxelPlayEnvironment.CHUNK_HALF_SIZE - VoxelPlayGreedyCommon.PADDING;
					pos.z = q1.y - VoxelPlayEnvironment.CHUNK_HALF_SIZE + h;
					vertices.Add (pos);
					pos.x += w;
					vertices.Add (pos);
					pos.x -= w;
					pos.z -= h;
					vertices.Add (pos);
					pos.x += w;
					vertices.Add (pos);
					break;
				case FaceDirection.Bottom:
					pos.y = slice - VoxelPlayEnvironment.CHUNK_HALF_SIZE;
					pos.x = q1.x - VoxelPlayEnvironment.CHUNK_HALF_SIZE - VoxelPlayGreedyCommon.PADDING;
					pos.z = q1.y - VoxelPlayEnvironment.CHUNK_HALF_SIZE - VoxelPlayGreedyCommon.PADDING;
					vertices.Add (pos);
					pos.x += w;
					vertices.Add (pos);
					pos.x -= w;
					pos.z += h;
					vertices.Add (pos);
					pos.x += w;
					vertices.Add (pos);
					break;
				case FaceDirection.Left:
					pos.x = slice - VoxelPlayEnvironment.CHUNK_HALF_SIZE;
					pos.z = q1.x - VoxelPlayEnvironment.CHUNK_HALF_SIZE + w;
					pos.y = q1.y - VoxelPlayEnvironment.CHUNK_HALF_SIZE - VoxelPlayGreedyCommon.PADDING;
					vertices.Add (pos);
					pos.y += h;
					vertices.Add (pos);
					pos.y -= h;
					pos.z -= w;
					vertices.Add (pos);
					pos.y += h;
					vertices.Add (pos);
					break;
				case FaceDirection.Right:
					pos.x = slice - (VoxelPlayEnvironment.CHUNK_HALF_SIZE - 1);
					pos.z = q1.x - VoxelPlayEnvironment.CHUNK_HALF_SIZE - VoxelPlayGreedyCommon.PADDING;
					pos.y = q1.y - VoxelPlayEnvironment.CHUNK_HALF_SIZE - VoxelPlayGreedyCommon.PADDING;
					vertices.Add (pos);
					pos.y += h;
					vertices.Add (pos);
					pos.z += w;
					pos.y -= h;
					vertices.Add (pos);
					pos.y += h;
					vertices.Add (pos);
					break;
				case FaceDirection.Back:
					pos.z = slice - VoxelPlayEnvironment.CHUNK_HALF_SIZE;
					pos.x = q1.x - VoxelPlayEnvironment.CHUNK_HALF_SIZE - VoxelPlayGreedyCommon.PADDING;
					pos.y = q1.y - VoxelPlayEnvironment.CHUNK_HALF_SIZE - VoxelPlayGreedyCommon.PADDING;
					vertices.Add (pos);
					pos.y += h;
					vertices.Add (pos);
					pos.x += w;
					pos.y -= h;
					vertices.Add (pos);
					pos.y += h;
					vertices.Add (pos);
					break;
				case FaceDirection.Forward:
					pos.z = slice - (VoxelPlayEnvironment.CHUNK_HALF_SIZE - 1);
					pos.x = q1.x - VoxelPlayEnvironment.CHUNK_HALF_SIZE + w;
					pos.y = q1.y - VoxelPlayEnvironment.CHUNK_HALF_SIZE - VoxelPlayGreedyCommon.PADDING;
					vertices.Add (pos);
					pos.y += h;
					vertices.Add (pos);
					pos.x -= w;
					pos.y -= h;
					vertices.Add (pos);
					pos.y += h;
					vertices.Add (pos);
					break;
				}
				indices.Add (index);
				indices.Add (index + 1);
				indices.Add (index + 2);
				indices.Add (index + 3);
				indices.Add (index + 2);
				indices.Add (index + 1);	
				index += 4;
			}

			// Clear for next usage
			qqCount = 0;
		}

	}
}