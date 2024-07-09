using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LogansFootLogicSystem
{
	public static class LFLS_Utilities
	{
		public static Vector3 FlatVector(Vector3 v)
		{
			return new Vector3( v.x, 0f, v.z );
		}
	}

	public enum LFLS_Mode
	{
		Force,
		Discrete,
		Hybrid
	}

	public enum LFLS_FootState
	{
		Grounded,
		Sliding,
		Airborn,

	}

}