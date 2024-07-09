using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LogansFootLogicSystem
{
	/// <summary>
	/// Optional sample script to use for quick and easy character movement with the foot system, or for reference on how 
	/// to use the foot system.
	/// </summary>
    public class CharacterMoveExample : MonoBehaviour
    {
		[Header("[-------- REFERENCE ---------]")]

		[SerializeField] FootSystem footSystem;
		public Transform Trans_perspective;
		public Transform trans_camera;

		[Header("[-------- STATS ---------]")]
		public float WalkSpeed = 7.7f;
		public float RunSpeed = 9.7f;
		public float RotSpeed = 200f;

		protected Vector3 minLookDown, maxLookUp;
		[SerializeField, Range(-0.99f, -0.1f)] protected float MinLookDown = -0.8f;
		[SerializeField, Range(0.1f, 0.99f)] protected float MaxLookUp = 0.910f;

		private bool amHoldingRunKey = false;

		void Start()
		{
			footSystem.Init(
				GetComponent<Transform>(), Trans_perspective, GetComponent<Rigidbody>(), LayerMask.GetMask("lr_EnvSolid")
				);

			RecalculateLookValues();
		}

		void LateUpdate()
		{
			float mouseX = Input.GetAxis("Mouse X");
			float mouseY = Input.GetAxis("Mouse Y");

			if ( Mathf.Abs(mouseY) > 0 )
			{
				trans_camera.Rotate( Vector3.right, RotSpeed * -mouseY * Time.deltaTime );

				if ( trans_camera.forward.y < MinLookDown )
				{
					trans_camera.LookAt( trans_camera.position + (Trans_perspective.rotation * minLookDown) );
				}
				else if ( trans_camera.forward.y > maxLookUp.y )
				{
					trans_camera.LookAt( trans_camera.position + (Trans_perspective.rotation * maxLookUp) );

				}
			}

			amHoldingRunKey = false;
			if ( Input.GetKeyDown(KeyCode.LeftShift) )
			{
				amHoldingRunKey = true;
			}

			if ( Input.GetKeyDown(KeyCode.Space) && footSystem.MyFootState == LFLS_FootState.Grounded )
			{
				footSystem.Jump();
			}

			footSystem.UpdateValues(
				amHoldingRunKey ? RunSpeed : WalkSpeed, Input.GetAxis("Vertical"), Input.GetAxis("Horizontal"), 
				RotSpeed * mouseX * Time.deltaTime
				);
		}

		[ContextMenu("call RecalculateLookValues()")]
		protected void RecalculateLookValues()
		{
			minLookDown = new Vector3(0f, MinLookDown, Mathf.Sqrt(Mathf.Pow(1f, 2f) - Mathf.Pow(MinLookDown, 2f)));// I don't need to multiply anything by 10 because of imaginary numbers, I believe...
			maxLookUp = new Vector3(0f, MaxLookUp, Mathf.Sqrt(Mathf.Pow(1f, 2f) - Mathf.Pow(MaxLookUp, 2f)));

		}
	}
}
