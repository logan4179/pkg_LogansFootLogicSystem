using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Windows;

namespace LogansFootLogicSystem
{
    public class FootSystem : MonoBehaviour
    {
		public LFLS_Mode Mode;
		[Space(10f)]

		//[Header("[-------- REFERENCE (INTERNAL) ---------]")]
		protected Transform trans_entity;
		protected Transform trans_perspective;
		protected Rigidbody rb;

		//[Header("[-------- STATE ---------]")]
		protected LFLS_FootState myFootState;
		public LFLS_FootState MyFootState => myFootState;

		//[Header("[-------- GENERAL STATS ---------]")]


		[Header("[-------- GROUND CHECKING ---------]")]
		[Tooltip("Force used to resist the rigidbody momentum, only when grounded. Similar to rigidbody friction, but managed more like character movement.")]
		private float force_MomentumResistance = 0.4f;
		[SerializeField, Range(0f, 1f), Tooltip("How upright the normal of the ground below the character needs to be in order to be considered 'stable'. When 'stable', the character naturally applies momentum resistance.")]
		private float thresh_groundedNormal = 0.85f;
		//-------------------------------------------------------
		private float dist_jumpSphereVerticalOffset;
		private float radius_jumpSphere;
		private Vector3 v_groundNormal;

		[Header("[-------- JUMPING ---------]")]
		public float JumpForce = 570f;
		[Tooltip("Controls how much the horizontal movement will bias the jump")] 
		public float HorizontalJumpForceBias = 0.1f; //todo: I feel like this should be made to be a range from 0 to 1


		[Header("[-------- AIRBORN LOCOMOTION ---------]")]
		public float Force_airbornMovement = 130f;
		public float Threshold_MaxFlatAirbornVelocity = 6.2f;

		[Tooltip("Amount of force in the y direction needed to trigger the land animation. Note that land force is measured negatively, so a negative number means more force required to trigger the landing condition")]
		public float Threshold_landForce = 8f;
		private int mask_Walkable;
		/// <summary>
		/// Mask for things that should be considered by the footsystem to potentially effect the foot state.
		/// </summary>
		public int Mask_Walkable { get {return mask_Walkable;} set {mask_Walkable = value;} }

        [HideInInspector] public UnityEvent OnJump;
		[HideInInspector] public UnityEvent OnLeaveGround;
		[HideInInspector] public UnityEvent OnLand;

		//[Header("[-------- INPUT ---------]")]
		private float axialInputValue = 0f;
		private float lateralInputValue = 0f;
		private Vector3 v_MoveInput = Vector3.zero;
		private float moveInputMagnitude;


		/// <summary>
		/// Speed that this system should move at.
		/// </summary>
		private float currentTargetSpeed;
		private Vector3 v_rbFlatVelocity;

		private float currentHorizontalSpeed = 0f;
		/// <summary>
		/// Keeps track of the horizontal speed this entity is moving.
		/// </summary>
		public float CurrentHorizontalSpeed => currentHorizontalSpeed;

		private void OnEnable()
		{
			OnJump = new UnityEvent();
			OnLeaveGround = new UnityEvent();
			OnLand = new UnityEvent();
		}

		private void OnDisable()
		{
			OnJump.RemoveAllListeners();
			OnLeaveGround.RemoveAllListeners();
			OnLand.RemoveAllListeners();
		}

		private void Awake()
		{
			SphereCollider col = GetComponent<SphereCollider>();
			radius_jumpSphere = col.radius * transform.localScale.x * 1.05f;
			dist_jumpSphereVerticalOffset = col.transform.localPosition.y;
		}

		/// <summary>
		/// Initializes the foot system. Call this inside the Start() for the player/entity script that this footsystem will serve.
		/// </summary>
		/// <param name="rootTransform">The transform at the root of the character heirarchy. The transform that should actually translate in response to movement input</param>
		/// <param name="perspectiveTransform">The transform for the perspective object that will allow rotation.</param>
		/// <param name="entityRigidBody"></param>
		/// <param name="mask"></param>
		public void Init( Transform rootTransform, Transform perspectiveTransform, Rigidbody entityRigidBody, int mask )
		{
			trans_entity = rootTransform;
			trans_perspective = perspectiveTransform;
			rb = entityRigidBody;
			mask_Walkable = mask;
		}

		/// <summary>
		/// "Drives" the entity by suppliying the movement input values as well as the current target speed for the 
		/// entity. Call this method in an outside script's update method.
		/// </summary>
		/// <param name="targetSpeed"></param>
		/// <param name="axialMoveInput">Typically the "forward-backward", or "front-to-back" movement input value</param>
		/// <param name="lateralMoveInput">Typically the "horizontal", or "sideways" movement input value.</param>
		/// <param name="hRotAmount">Horizontal rotation input value.</param>
		public void UpdateValues( float targetSpeed, float axialMoveInput, float lateralMoveInput, float hRotAmount )
		{
			currentTargetSpeed = targetSpeed;
			axialInputValue = axialMoveInput;
			lateralInputValue = lateralMoveInput;
			v_MoveInput = (trans_perspective.forward * axialMoveInput) + (trans_perspective.right * lateralInputValue);
			moveInputMagnitude = Mathf.Max(Mathf.Abs(axialInputValue), Mathf.Abs(lateralInputValue));
			trans_perspective.Rotate(Vector3.up, hRotAmount);
		}

		protected bool flag_justJumped;
		/// <summary>
		/// Causes this entity to jump using rigidbody physics.
		/// </summary>
		/// <param name="axialMoveInput">Commonlhy known as "forward facing" movement input</param>
		/// <param name="lateralMoveInput">Commonlhy known as "side facing" movement input</param>
		/// <param name="amHoldingRun">Whether the run key is being held</param>

		public void Jump()
		{
			float MoveInputMagnitude = Mathf.Max( Mathf.Abs(axialInputValue), Mathf.Abs(lateralInputValue) );
			rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z); //This is so that you always jump the same height

			Vector3 v_jumpDir = (trans_entity.up * JumpForce) + (v_MoveInput.normalized * Mathf.Clamp(currentHorizontalSpeed, 0f, currentTargetSpeed) * HorizontalJumpForceBias);
			rb.AddForce( v_jumpDir, ForceMode.Impulse );

			OnJump.Invoke();
			flag_justJumped = true;
		}

		private Vector3 lastPos;
		protected void LateUpdate()
		{
			#region UPDATE FOOTING---------------//////////////////
			LFLS_FootState oldFootState = myFootState;

			Vector3 v_sphereStart = trans_entity.position + Vector3.up * dist_jumpSphereVerticalOffset;
			if ( Physics.CheckSphere(v_sphereStart, radius_jumpSphere, mask_Walkable) )
			{
				myFootState = LFLS_FootState.Grounded;

				RaycastHit hitInfo = new RaycastHit();
				if ( Physics.Linecast(v_sphereStart, v_sphereStart + (Vector3.down * radius_jumpSphere * 1.05f), out hitInfo, mask_Walkable) )
				{
					v_groundNormal = hitInfo.normal;
					if ( v_groundNormal.y < thresh_groundedNormal )
					{
						myFootState = LFLS_FootState.Sliding;
					}
				}
				else
				{
					v_groundNormal = Vector3.up;
				}
			}
			else
			{
				myFootState = LFLS_FootState.Airborn;
			}

			if ( myFootState == LFLS_FootState.Airborn && oldFootState != LFLS_FootState.Airborn )
			{
				Vector3 v_flatVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
				rb.AddForce(v_flatVelocity.normalized, ForceMode.VelocityChange);
			}
			#endregion

			currentHorizontalSpeed = (1 / Time.deltaTime) * LFLS_Utilities.FlatVector(lastPos - trans_entity.position).magnitude;
			lastPos = trans_entity.position;
			flag_justJumped = false; //resets here every frame
		}

		private Vector3 v_counterForce = Vector3.zero;
		private void FixedUpdate()
		{
			v_rbFlatVelocity = LFLS_Utilities.FlatVector(rb.velocity);
			float dot_rbVelRelativeToMoveInput = Vector3.Dot(v_rbFlatVelocity.normalized, v_MoveInput.normalized);
			v_counterForce = Vector3.zero;

			if ( moveInputMagnitude > 0f )
			{
				if ( myFootState == LFLS_FootState.Grounded || myFootState == LFLS_FootState.Sliding )
				{
					Vector3 desiredMove = Vector3.ProjectOnPlane(v_MoveInput, v_groundNormal).normalized;
					rb.MovePosition( 
						trans_entity.position + (desiredMove.normalized * currentTargetSpeed * moveInputMagnitude * Time.fixedDeltaTime)
						);
				}
				else if ( myFootState == LFLS_FootState.Airborn )
				{
					//The following prevents the player from building up too much horizontal movement speed if a direction is held while falling for a long time...
					#region AIRBORN VELOCITY CORRECTION-----------////////////
					float dotProductMultiplier = 1f;
					if ( v_rbFlatVelocity.magnitude > Threshold_MaxFlatAirbornVelocity )
					{
						if ( dot_rbVelRelativeToMoveInput >= 0f )
						{
							dotProductMultiplier = Mathf.Max( 0f, 1 - dot_rbVelRelativeToMoveInput );
						}
					}
					#endregion

					rb.AddForce( v_MoveInput * Force_airbornMovement * 10f * dotProductMultiplier * Time.fixedDeltaTime, ForceMode.Acceleration );

				}
			}

			if ( myFootState == LFLS_FootState.Grounded && v_rbFlatVelocity.magnitude > 0.05f )
			{
				v_counterForce = Vector3.ClampMagnitude(-v_rbFlatVelocity, force_MomentumResistance);
				rb.AddForce(v_counterForce * 1000f, ForceMode.Force);
				//if (DbgPrint) print($"resisting with: '{v_counterForce}'");
			}

			currentHorizontalSpeed = (1 / Time.deltaTime) * LFLS_Utilities.FlatVector(lastPos - trans_entity.position).magnitude;
			lastPos = trans_entity.position;
		}
	}
}