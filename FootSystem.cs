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
		[SerializeField, Tooltip("The root Transform that you want the foot system to move")]
		protected Transform trans_root;
		[SerializeField, Tooltip("The Transform that you want the foot system to rotate")]
		protected Transform trans_rotate;
		protected Rigidbody rb;

		//[Header("[-------- STATE ---------]")]
		protected LFLS_FootState myFootState;
		public LFLS_FootState MyFootState => myFootState;
		/// <summary>Position that is considered directly underneath this foot system </summary>
		protected Vector3 currentGroundPos;
		/// <summary>Position that is considered directly underneath this foot system </summary>
		public Vector3 CurrentGroundPos => currentGroundPos;

		/// <summary>Normal describing geometry currently underfoot. </summary>
		protected Vector3 currentGroundNormal;
		/// <summary>Normal describing geometry currently underfoot. </summary>
		public Vector3 CurrentGroundNormal => currentGroundNormal;

		//[Header("[-------- GENERAL STATS ---------]")]


		[Header("[-------- GROUND CHECKING ---------]")]
		[Tooltip("Force used to resist the rigidbody momentum, only when grounded. Similar to rigidbody friction, but managed more like character movement.")]
		protected float force_MomentumResistance = 0.4f;
		[SerializeField, Range(0f, 1f), Tooltip("How upright the normal of the ground below the character needs to be in order to be considered 'stable'. When 'stable', the character naturally applies momentum resistance.")]
		protected float thresh_groundedNormal = 0.85f;
		//-------------------------------------------------------
		protected float cachedRadius_jumpSphere;


		[Header("[-------- JUMPING ---------]")]
		public float JumpForce = 570f;
		[Tooltip("Controls how much the horizontal movement will bias the jump")] 
		public float HorizontalJumpForceBias = 0.1f; //todo: I feel like this should be made to be a range from 0 to 1


		[Header("[-------- AIRBORN LOCOMOTION ---------]")]
		public float Force_airbornMovement = 130f;
		public float Threshold_MaxFlatAirbornVelocity = 6.2f;

		[Tooltip("Amount of force in the y direction needed to trigger the land animation. Note that land force is measured negatively, so a negative number means more force required to trigger the landing condition")]
		public float Threshold_landForce = 8f;
		[Tooltip("Layer mask that represents 'walkable ground' in your game")]
		public int mask_Walkable;
		/// <summary>
		/// Mask for things that should be considered by the footsystem to potentially effect the foot state.
		/// </summary>
		public int Mask_Walkable { get {return mask_Walkable; } set { mask_Walkable = value;} }

        [HideInInspector] public UnityEvent OnJump;
		[HideInInspector] public UnityEvent OnLeaveGround;
		[HideInInspector] public UnityEvent OnLand;

		//[Header("[-------- INPUT ---------]")]
		protected float axialInputValue = 0f;
		protected float lateralInputValue = 0f;
		protected Vector3 v_MoveInput = Vector3.zero;
		protected float moveInputMagnitude;


		/// <summary>
		/// Speed that this system should move at.
		/// </summary>
		protected float targetSpeed_cached;

		protected float hzntlRotAmt_cached;

		protected bool rotateImmediate_cached = true;

		protected Vector3 v_rbFlatVelocity;

		protected float currentHorizontalSpeed = 0f;
		/// <summary>
		/// Keeps track of the horizontal speed this entity is moving.
		/// </summary>
		public float CurrentHorizontalSpeed => currentHorizontalSpeed;
		protected float currentSpeed = 0f;
		public float CurrentSpeed => currentSpeed;

		[Header("[------ DEBUG ------]")]
		[SerializeField] private string DbgState;

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

		protected void Awake()
		{
			rb = trans_root.GetComponent<Rigidbody>();

			SphereCollider col = GetComponent<SphereCollider>();
			cachedRadius_jumpSphere = col.radius * transform.localScale.x * 1.1f;
		}

		/// <summary>
		/// "Drives" the entity by suppliying the movement input values as well as the current target speed for the 
		/// entity. Call this method in an outside script's update method.
		/// </summary>
		/// <param name="targetSpeed">Speed per second to move the rigidbody</param>
		/// <param name="axialMoveInput">Typically the "forward-backward", or "front-to-back" movement input value</param>
		/// <param name="lateralMoveInput">Typically the "horizontal", or "sideways" movement input value.</param>
		/// <param name="hRotAmount">Horizontal rotation input value.</param>
		public void UpdateValues(float targetSpeed, float axialMoveInput, float lateralMoveInput, float hRotAmount, bool rotateImmediate = true )
		{
			targetSpeed_cached = targetSpeed;
			axialInputValue = axialMoveInput;
			lateralInputValue = lateralMoveInput;

			v_MoveInput = 
				(trans_rotate.forward * axialMoveInput) +
				(trans_rotate.right * lateralInputValue);

			moveInputMagnitude = Mathf.Max(Mathf.Abs(axialInputValue), Mathf.Abs(lateralInputValue));

			if( rotateImmediate )
			{
				trans_rotate.Rotate(Vector3.up, hRotAmount);
			}
            else
            {
                hzntlRotAmt_cached = hRotAmount;
            }
		}

		/// <summary>
		/// "Drives" the entity by supplying movement and rotation values. This overload allows you to move 
		/// in the direction of a separate supplied transform, such as a third person camera following a character.
		/// </summary>
		/// <param name="targetSpeed"></param>
		/// <param name="axialMoveInput"></param>
		/// <param name="lateralMoveInput"></param>
		/// <param name="hRotAmount"></param>
		/// <param name="perspectiveForward"></param>
		/// <param name="perspectiveRight"></param>
		public void UpdateValues( 
			float targetSpeed, float axialMoveInput, float lateralMoveInput, float hRotAmount, Vector3 perspectiveForward, Vector3 perspectiveRight, bool rotateImmediate = true
			)
		{
			targetSpeed_cached = targetSpeed;
			axialInputValue = axialMoveInput;
			lateralInputValue = lateralMoveInput;

			v_MoveInput =
				(perspectiveForward * axialMoveInput) +
				(perspectiveRight * lateralInputValue);

			moveInputMagnitude = Mathf.Max( Mathf.Abs(axialInputValue), Mathf.Abs(lateralInputValue) );
			if (rotateImmediate)
			{
				trans_rotate.Rotate(Vector3.up, hRotAmount);
			}
			else
			{
				hzntlRotAmt_cached = hRotAmount;
			}
		}

		public void TravelToward( Vector3 pos, float distThresh, float mvSpd, float rotAngThresh, float rotSpd, Vector3 rotNrml )
		{
			Vector3 v_toGoal = Vector3.Normalize( pos - trans_root.position );

			Vector3 v_nrml_calculated = Vector3.RotateTowards(
				trans_rotate.up, rotNrml, rotSpd * Time.fixedDeltaTime, 0f
			);
			float dot_facingToPos = Vector3.Dot( trans_rotate.forward, v_toGoal );
			float dot_upToNrml = Vector3.Dot( trans_rotate.up, rotNrml );

			Quaternion q = Quaternion.identity;
			Vector3 vRot = Vector3.zero;
			if ( dot_facingToPos < -0.98f && dot_upToNrml > 0.95f ) //The idea of this block is that it forces the transform to rotate 'rigthwards' if we're pretty much facing the opposite direction of the goal, that way you don't get weird rotating
			{
				//vRot = Vector3.RotateTowards(trans.forward, v_toGoal, rotSpeed_passed * Time.fixedDeltaTime, 0.0f);
				vRot = Vector3.RotateTowards(trans_rotate.forward, trans_rotate.right, rotSpd * Time.fixedDeltaTime, 0.0f);
				q = Quaternion.LookRotation(vRot, trans_rotate.up);
			}
			else
			{
				//vRot = Vector3.RotateTowards(trans.forward, v_toGoal, rotSpeed_passed * Time.fixedDeltaTime, 0.0f);
				vRot = Vector3.RotateTowards(trans_rotate.forward, v_toGoal, rotSpd * Time.fixedDeltaTime, 0.0f);
				q = Quaternion.LookRotation(vRot, v_nrml_calculated);
			}

			//rb.MoveRotation(q);
			//UpdateValues(mvSpd, float axialMoveInput, float lateralMoveInput, float hRotAmount, bool rotateImmediate = true)

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

			Vector3 v_jumpDir = (trans_root.up * JumpForce) + (v_MoveInput.normalized * Mathf.Clamp(currentHorizontalSpeed, 0f, targetSpeed_cached) * HorizontalJumpForceBias);
			rb.AddForce( v_jumpDir, ForceMode.Impulse );

			OnJump.Invoke();
			flag_justJumped = true;
		}

		protected Vector3 lastPos;
		protected void LateUpdate()
		{
			#region UPDATE FOOTING---------------//////////////////
			LFLS_FootState oldFootState = myFootState;

			DbgState = $"";
			Vector3 v_sphereStart = trans_root.position + trans_root.up * transform.localPosition.y; 
			if ( Physics.CheckSphere(/*v_sphereStart*/transform.localPosition, cachedRadius_jumpSphere, Mask_Walkable) )
			{
				DbgState += $"checksphere success\n";
				myFootState = LFLS_FootState.Grounded;
				
				RaycastHit hitInfo = new RaycastHit();
				if ( Physics.Linecast(v_sphereStart, v_sphereStart + (Vector3.down * cachedRadius_jumpSphere * 1.05f), out hitInfo, Mask_Walkable) )
				{
					DbgState += $"linecast success\n";

					currentGroundPos = hitInfo.point;
					currentGroundNormal = hitInfo.normal;
					if ( currentGroundNormal.y < thresh_groundedNormal )
					{
						myFootState = LFLS_FootState.Sliding;
					}
				}
				else
				{
					DbgState += $"linecast failed \n";

					currentGroundNormal = Vector3.up;
					currentGroundPos = rb.position;
				}
			}
			else
			{
				myFootState = LFLS_FootState.Airborn;
				DbgState += $"checksphere failed \n";

			}

			if ( myFootState == LFLS_FootState.Airborn && oldFootState != LFLS_FootState.Airborn )
			{
				Vector3 v_flatVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
				rb.AddForce(v_flatVelocity.normalized, ForceMode.VelocityChange);
			}
			#endregion

			currentSpeed = Vector3.Distance(trans_root.position, lastPos) / Time.deltaTime;
			currentHorizontalSpeed = (1 / Time.deltaTime) * LFLS_Utilities.FlatVector(lastPos - trans_root.position).magnitude;
			lastPos = trans_root.position;
			flag_justJumped = false; //resets here every frame

			DbgState += $"myFootState: '{myFootState}'\n" +
				$"{nameof(currentGroundPos)}: '{currentGroundPos}'\n" +
				$"{nameof(currentGroundNormal)}: '{currentGroundNormal}'\n" +
				$"{nameof(cachedRadius_jumpSphere)}: '{cachedRadius_jumpSphere}'\n" +
				$"{nameof(Mask_Walkable)}: '{Mask_Walkable}'\n" +
				$"";
		}

		private Vector3 v_counterForce = Vector3.zero;
		protected void FixedUpdate()
		{
			v_rbFlatVelocity = LFLS_Utilities.FlatVector(rb.velocity);
			float dot_rbVelRelativeToMoveInput = Vector3.Dot(v_rbFlatVelocity.normalized, v_MoveInput.normalized);
			v_counterForce = Vector3.zero;

			if( !rotateImmediate_cached )
			{
				trans_rotate.Rotate( Vector3.up, hzntlRotAmt_cached * Time.fixedDeltaTime );
			}

			if ( moveInputMagnitude > 0f )
			{
				if ( myFootState == LFLS_FootState.Grounded || myFootState == LFLS_FootState.Sliding )
				{
					Vector3 projectedMove = Vector3.ProjectOnPlane(v_MoveInput, currentGroundNormal).normalized;
					rb.MovePosition( 
						trans_root.position + (projectedMove.normalized * targetSpeed_cached * moveInputMagnitude * Time.fixedDeltaTime)
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
		}

		public void OrientRoot( Vector3 normal )
		{

		}

		private void OnDrawGizmos()
		{
			
		}

		public bool CheckIfKosher()
		{
			if ( trans_root == null || rb == null )
			{
				return false;
			}

			return true;
		}
	}
}