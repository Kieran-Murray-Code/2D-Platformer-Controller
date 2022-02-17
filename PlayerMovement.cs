using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Prime31;
using TLODC_Scripts.Player.Messages;
using UnityEngine.UI;

[SelectionBase]
public class PlayerMovement : MonoBehaviourEx, IHandle<OnJumpInput>,  IHandle<OnMovementInput>,  IHandle<OnCrouchInput>, 
IHandle<OnInteractInput>,  IHandle<OnLevelReset>,  IHandle<OnLevelLoad>,  IHandle<OnPlayerDamaged>,  IHandle<OnTopOfMovingPlatform>, 
IHandle<OnSideOfMovingPlatform>,IHandle<OnAimLocked>,  IHandle<OnBottomOfMovingPlatform>,  IHandle<OnCheckpointSave>,  
IHandle<OnCheckpointLoadStarted>,  IHandle<OnCheckpointLoadEnded>, IHandle<OnAim>, IHandle<OnPlayerControlsDisabled>,IHandle<OnPlayerControlsEnabled>, 
	IHandle<OnTouchingAConveyorBelt>,IHandle<OnMovementLockToggled>
{

	// Variables
	#region

	public enum CharacterType{Danny,Discman}
	public CharacterType characterType;
	public PlayerMovementSettings discmanMovementSettiings;
	public PlayerMovementSettings dannyMovementSettings;
	public PlayerMovementVariables variables;


	private float appliedTerminalVelocity = 0.0f;
	private bool hasReachedJumpApex = false;
	private bool isFalling = false;
	private bool isFallingLongDistance = false;
	private float appliedAcceleration = 0.0f;
	private float appliedDeceleration = 0.0f;
	private bool stickToWall = true;
	private bool isPullingOffWall = false;
	private float appliedGravity = 0.0f;
	public bool UseGravity = true;
	private	float appliedWallJumpStrength = 0;
	private float movementSpeed = 0.0f;
	private float appliedSpeed = 0.0f;
	private float targetSpeed = 0;
	private float jumpInputTimeHeld = 0.0f;
	private float jumpInputEndTime = 0.0f;
	public CharacterController2D controller;
	public bool isGrounded = false;
	public bool isJumping = false;
	private bool isDoubleJumping = false;
	private bool isTouchingCeiling = false;
	private bool isWallJumping = false;
	private bool isTouchingWall = false;
	private bool isTouchingRightWall = false;
	private bool isTouchingLeftWall = false;
	private bool isOnRightSideOfMovingPlatform = false;
	private bool isOnLeftSideOfMovingPlatform = false;
	private bool groundedDelayCoroutineIsRunning = false;
	private Coroutine pullOffWallCoroutine;
	private float fallTime = 0.0f;
	private bool doubleJumpBufferIsRunning = false;
	private Coroutine doubleJumpBufferCoroutine;
	private Coroutine jumpInputBufferingCoroutine;
	private Coroutine jumpCoroutine;
	private Coroutine wallJumpCoroutine;
	private Coroutine doubleJumpCoroutine;
	private bool isCrouching = false;
	private bool isStandingUp = false;
	private bool isGroundSliding = false;
	private bool isAiming = false;
	private bool isWallSliding = false;
	private bool isPushing = false;
	private bool isOnMovingPlatform = false;
	private bool isMoving = false;

	// Input
	private bool jumpInput = false;
	private bool movementInput = false;
	private int xDirection = 1;
	private int movementDirection = 1;
	private bool crouchInput = false;
	private bool interactInput = false;

	private BoxCollider2D boxCollider;

	private string lastMessageSent = "Nothing Sent Yet";

	private Transform tf;
	private Rigidbody2D rb;

	private Coroutine crouchCoroutine;
	private Coroutine standUpCoroutine;
	private bool canWallJump = false;


	private float widthOffset = 0;
	private float wallDetectedTime = 0.0f;
	private int wallJumpDirection = 0;
	private float aimHeldFor = 0.0f;

	private Vector2 rbVelocity;
	private Vector2 rbPositionLastFrame;

	private Coroutine resetPlatformVelocityCoroutine;

	private Vector3 checkpointSpawnPosition;

	public Vector3 velocity;
	private Vector3 movingPlatformVelocity;
	private Vector3 combinedForces;
	private bool _lockMovementWhileAiming = true;

	private float _yPosOnJumpStart = 0.0f;
	private float _yPosOnJumpApexReached = 0.0f;
	[HideInInspector]
	public float JumpHeight = 0.0f;

	private bool canJump = true;

	#endregion

	void Start()
	{
		LoadSettings ();

		if (PlayerPrefs.GetInt("LockMovementOnAim") == 1)
		{
			_lockMovementWhileAiming = true;
		}
		else
		{
			_lockMovementWhileAiming = false;
		}
		
		if (GetComponent<CharacterController2D> () != null)
		{
			controller = GetComponent<CharacterController2D> ();
//			controller.onControllerCollidedEvent += OnControllerCollider;
		}

		boxCollider = GetComponent<BoxCollider2D> ();
		tf = this.transform;
		rb = GetComponent<Rigidbody2D> ();
		rbPositionLastFrame = rb.position;
		widthOffset = (boxCollider.size.x/2) * tf.localScale.x;

		discmanMovementSettiings.variables.movementEnabled = true;
		discmanMovementSettiings.variables.jumpEnabled = true;
		dannyMovementSettings.variables.movementEnabled = true;
		dannyMovementSettings.variables.jumpEnabled = true;
	}

	void OnEnable()
	{
		Messenger.Subscribe (this);
	}

	void OnDisable()
	{
		Messenger.Unsubscribe (this);
		StopAllCoroutines ();
	}

	public void Handle(OnMovementLockToggled message)
	{
		_lockMovementWhileAiming = message.ToggleMovementLockWhileAiming;
	}
	
	public void Handle(OnCheckpointSave message)
	{
		checkpointSpawnPosition = message.CheckpointSpawnInfo.playerSpawnPoint.position;
	}

	public void Handle(OnCheckpointLoadStarted message)
	{
		DisableControls ();
		tf.position = checkpointSpawnPosition;
	}

	public void DisableControls()
	{
		velocity = Vector3.zero;
        movingPlatformVelocity = Vector3.zero;
        isOnMovingPlatform = false;
		tf.parent = null;
        appliedSpeed = 0;
		variables.movementEnabled = false;
		variables.jumpEnabled = false;
        isAiming = false;
	}

    public void Handle(OnTouchingAConveyorBelt messsage)
    {
        if(messsage.ConveyorVelocity.y != 0)
        { 
          if(isTouchingWall)
            {
               movingPlatformVelocity = messsage.ConveyorVelocity;
            }
            else
            {
                movingPlatformVelocity = Vector3.zero;
            }
        }
        else
        {
            movingPlatformVelocity = messsage.ConveyorVelocity;
        }
    }

    public void Handle(OnPlayerControlsDisabled message)
    {
        DisableControls();
    }
    public void Handle(OnPlayerControlsEnabled message)
    {
        EnableControls();
    }

	public void EnableControls()
	{
		variables.movementEnabled = true;
		variables.jumpEnabled = true;
	}

	public void Handle(OnCheckpointLoadEnded message)
	{
		EnableControls ();
	}

	public void Handle(OnBottomOfMovingPlatform message)
	{
		if (message.IsTouchingBottomOfMovingPlatform)
		{
			if(message.PlatformVelocity.y < 0)
			{
				if(controller.isGrounded)
				{
//					Destroy (gameObject);
				}
			}
		}
		else
		{
			
		}
	}
	public void Handle(OnSideOfMovingPlatform message)
	{
		if (message.LockXPosition == true)
		{
			if (message.Wall == "left")
			{
				isOnLeftSideOfMovingPlatform = true;
				if(message.PlatformVelocity.x < 0)
				{
					if(isTouchingLeftWall)
					{
//						Destroy (gameObject);
					}
					movingPlatformVelocity = new Vector3(message.PlatformVelocity.x,0,0);
				}
			}
			else if (message.Wall == "right")
			{
				isOnRightSideOfMovingPlatform = true;
				if(message.PlatformVelocity.x > 0)
				{
					if(isTouchingRightWall)
					{
//						Destroy (gameObject);
					}
					movingPlatformVelocity = new Vector3(message.PlatformVelocity.x,0,0);
				}
			}


			if(resetPlatformVelocityCoroutine != null)
			{
				StopCoroutine (resetPlatformVelocityCoroutine);
			}


		}
			
		else
		{
			movingPlatformVelocity = new Vector3(message.PlatformVelocity.x,0,0);
			isOnLeftSideOfMovingPlatform = false;
			isOnRightSideOfMovingPlatform = false;
			resetPlatformVelocityCoroutine = StartCoroutine (ResetMovingPlatformVelocity (0f));
		}


	}
	public void Handle(OnTopOfMovingPlatform message)
	{
		if (isJumping)
		{
			isOnMovingPlatform = false;
			movingPlatformVelocity = Vector3.zero;
		}
		else
		{
			isOnMovingPlatform = message.LockYPosition;
		}
		
		transform.parent = message.PlatformTransform;

		if (isOnMovingPlatform && isJumping == false)
		{
			if (message.PlatformVelocity.y > 0)
			{
	
				movingPlatformVelocity = new Vector3(message.PlatformVelocity.x,0.0f,0.0f);
				if (isTouchingCeiling)
				{
//					Destroy (gameObject);
				}
			}
			else
			{
				movingPlatformVelocity = new Vector3(message.PlatformVelocity.x,0.0f,0.0f);
			}
//			if(resetPlatformVelocityCoroutine != null)
//			{
//				StopCoroutine (resetPlatformVelocityCoroutine);
//			}
		}
		else
		{
			//movingPlatformVelocity = message.PlatformVelocity;
			//velocity.y = 0.0f;
//			resetPlatformVelocityCoroutine = StartCoroutine (ResetMovingPlatformVelocity (0.5f));
		}
	}

	IEnumerator ResetMovingPlatformVelocity(float delay)
	{
		yield return new WaitForSeconds (delay);
		movingPlatformVelocity = Vector3.zero;
	}

	public void Handle(OnPlayerDamaged message)
	{
	}

	public void Handle (OnLevelReset message)
	{
	}

	public void Handle(OnLevelLoad message)
	{
		Messenger.Unsubscribe (this);
		StopAllCoroutines ();
	}

	public void Handle(OnInteractInput message)
	{
		interactInput = message.InteractInput;
	}
    public void Handle(OnMovementInput message)
    {
/*        if (isWallSliding == false && canWallJump == false && isAiming == false && isWallJumping == false && message.DirectionHasChanged)
        {
            xDirection = message.MovementDirection;
	        movementDirection = message.MovementDirection;
        }

	    if (isWallSliding)
	    {
		    movementDirection = message.MovementDirection;
	    }*/

	    if (message.DirectionHasChanged)
	    {
		    if (isWallSliding == false)
		    {
				xDirection = message.MovementDirection;
				movementDirection = message.MovementDirection;
		    }
		    else
		    {
			    movementDirection = message.MovementDirection;
		    }
	    }

        if(isAiming == false)
        {
		    movementInput = message.MovementInput;
        }
	}



	public void Handle(OnAimLocked message)
	{
		if (message.IsLocked)
		{
			isAiming = true;
		}
		else
		{
			if(isAiming)
			{
                if(isGrounded || isTouchingWall)
                {
                    movementInput = false;
                }
				StartCoroutine (TransitionOutOfAimingMode());
			}	
		}
	}

	public void Handle(OnAim message)
	{
		if (_lockMovementWhileAiming)
		{
			if(Mathf.Abs(velocity.x) < 2f)
			{
				isAiming = true;
			}
		}
	}


	public IEnumerator TransitionOutOfAimingMode()
	{
		yield return new WaitForSeconds (0.1f);
		isAiming = false;
	}

	public void Handle(OnJumpInput message)
	{
		
		jumpInput = message.JumpInput;

		if (jumpInput) 
		{
			if (controller.isGrounded)
			{
				if (isJumping == false)
				{
					jumpCoroutine =	StartCoroutine (Jump ());
				}
			}
			else
			{
				if (jumpInputBufferingCoroutine != null)
				{
					StopCoroutine(jumpInputBufferingCoroutine);
				}
				jumpInputBufferingCoroutine = StartCoroutine (JumpInputBuffering());
			}
		}
		else 
		{
			if (isTouchingWall)
			{
				isJumping = false;
			}

			/*			isDoubleJumping = false;
			
			if (doubleJumpBufferIsRunning)
			{
				if(doubleJumpBufferCoroutine != null)
				{
					StopCoroutine (doubleJumpBufferCoroutine);
				}
			}
			else
			{
				if(isTouchingWall == false)
				{
					doubleJumpBufferCoroutine = StartCoroutine (DoubleJumpInputBuffering ());
				}
			}*/
		}
	}
	public void Handle(OnCrouchInput message)
	{
		crouchInput = message.CrouchInput;
		if (crouchInput)
		{
			if(isCrouching == false && controller.isGrounded)
			{
				if (movementInput)
				{
					if(isGroundSliding == false && variables.groundSlidingEnabled)
					{
						StartCoroutine (GroundSlide ());
					}
				}
				else
				{
					if(variables.crouchingEnabled && variables.controlsEnabled)
					{
						if(crouchCoroutine != null)
						{
							StopCoroutine (crouchCoroutine);
						}

						if(standUpCoroutine	!= null)
						{
							StopCoroutine (standUpCoroutine);
						}
						crouchCoroutine = StartCoroutine (Crouch ());
					}
				}
			}
		}
		else
		{
			if((isCrouching || isGroundSliding) && isStandingUp == false)
			{
				if(crouchCoroutine != null)
				{
					StopCoroutine (crouchCoroutine);
				}

				if(standUpCoroutine	!= null)
				{
					StopCoroutine (standUpCoroutine);
				}

				standUpCoroutine = StartCoroutine (StandUp());
			}
		}
	}

	IEnumerator GroundSlide()
	{
		isGroundSliding = true;
		Vector2 tmpSize =  boxCollider.size;
		tmpSize.y *= variables.crouchSize;
		tmpSize.x *= 2;
		boxCollider.size = tmpSize;
		Vector3 tmpPosition = transform.position;
		tmpPosition.y -= 0.225f;
		transform.position = tmpPosition;
		controller.recalculateDistanceBetweenRays ();
		yield return new WaitForEndOfFrame ();
	}

	IEnumerator DoubleJumpInputBuffering()
	{

		doubleJumpBufferIsRunning = true;
		float timeInDoubleJumpBuffer = 0.0f;

		if(variables.doubleJumpInpuBufferTime == 0)
		{
			variables.doubleJumpInpuBufferTime = Mathf.Infinity;
		}


		while(timeInDoubleJumpBuffer + Time.deltaTime < variables.doubleJumpInpuBufferTime)
		{

			if (jumpInput)
			{
				if (isDoubleJumping == false && variables.doubleJumpEnabled)
				{
					doubleJumpCoroutine = StartCoroutine(DoubleJump());
					isDoubleJumping = true;
				}
			}
			timeInDoubleJumpBuffer += Time.deltaTime;
			yield return new WaitForEndOfFrame ();
		}
		doubleJumpBufferIsRunning = false;
	}

	IEnumerator JumpInputBuffering()
	{	
		float timeInInputBuffer = 0.0f;
		
		if (Time.deltaTime > variables.jumpInputBufferTime)
		{
			if (controller.isGrounded) 
			{
				if (isCrouching == false)
				{
					if (jumpInput && variables.jumpEnabled)
					{
						if (isJumping == false && jumpCoroutine == null)
						{
							jumpCoroutine =	StartCoroutine (Jump ());
							isJumping = true;
						}
					}
				}
			} 
			else 
			{
				if (isCrouching == false && canWallJump && isTouchingRightWall && isWallJumping == false && variables.jumpEnabled && variables.wallJumpEnabled) 
				{
					wallJumpCoroutine =	StartCoroutine (WallJump());
				}
			}
		}
		else
		{
			while (timeInInputBuffer + Time.deltaTime < variables.jumpInputBufferTime)
			{
				if (isGrounded)
				{
					if (isCrouching == false)
					{
						if (jumpInput && variables.jumpEnabled)
						{
							if (isJumping == false && jumpCoroutine == null)
							{
								jumpCoroutine = StartCoroutine(Jump());
								isJumping = true;
							}
						}
					}
				}
				else
				{
					if (jumpInput && isCrouching == false && canWallJump && isTouchingWall &&
					    isWallJumping == false && variables.jumpEnabled && variables.wallJumpEnabled)
					{
						wallJumpCoroutine = StartCoroutine(WallJump());
					}
				}

				timeInInputBuffer += Time.deltaTime;
				yield return new WaitForEndOfFrame();
			}
		}
	}

	IEnumerator Crouch()
	{
		appliedSpeed = 0;
		isCrouching = true;
		Vector2 tmpSize =  boxCollider.size;
		Vector3 tmpPosition = transform.position;
		tmpSize.y *= variables.crouchSize;
		boxCollider.size = tmpSize;
		tmpPosition.y -= (tmpSize.y*tf.localScale.y)*variables.crouchSize;
		tmpPosition.y += controller.skinWidth+0.1f;
		transform.position = tmpPosition;
		controller.recalculateDistanceBetweenRays ();
		yield return new WaitForEndOfFrame();
	}

	IEnumerator StandUp()
	{

		isStandingUp = true;
		while(Physics2D.BoxCast(transform.position,new Vector2(0.48f,0.5f),0.0f,Vector2.up,0.5f))
		{
			yield return new WaitForEndOfFrame();
		}

		Messenger.Publish (new OnStandUp(xDirection));
		Vector3 tmpPosition = transform.position;
		Vector2 tmpSize =  boxCollider.size;
		tmpPosition.y += (tmpSize.y*tf.localScale.y)*variables.crouchSize;
		tmpPosition.y += controller.skinWidth;
		transform.position = tmpPosition;
		tmpSize.y /= variables.crouchSize;
		if(isGroundSliding)
		{
			tmpSize.x /= 2;
		}
		boxCollider.size = tmpSize;
		controller.recalculateDistanceBetweenRays ();
		yield return new WaitForEndOfFrame();
		isCrouching = false;
		isGroundSliding = false;
		isStandingUp = false;


	}



	void Update ()
	{	
		Vector3 tmpPos = tf.position;
        tmpPos.z = 0;
        tf.position = tmpPos;

		if (Mathf.Abs (velocity.x) > 0.1f)
		{
			isMoving = true;
		}
		else
		{
			isMoving = false;
		}

		CheckIfCanWallJump ();

		Messenger.Publish (new OnPlayerIsMoving(isMoving));
		Messenger.Publish (new OnPlayerFacingDirectionRecieved(xDirection));
		Messenger.Publish (new OnPlayerTransformRecieved(tf));
		if(variables.controlsEnabled)
		{
			MovementSpeedUpdate();
			AnimationMessageHandling ();
		}
//		rb.velocity = controller.velocity;

		

		if(isOnMovingPlatform == false)
		{
			
			if (isJumping && velocity.y - appliedGravity * Time.deltaTime <= 0)
			{
				isJumping = false;
				_yPosOnJumpApexReached = tf.position.y;
			}
			
/*			isFalling = IsFalling();
			isFallingLongDistance = IsFallingLongDistance();*/
		}




		if (controller.isGrounded)
		{
			if(doubleJumpBufferCoroutine != null)
			{
				StopCoroutine (doubleJumpBufferCoroutine);
			}

            if(isAiming == false)
            {
                movementInput = false;
            }
			doubleJumpBufferIsRunning = false;
			fallTime = 0.0f;
			hasReachedJumpApex = false;

			if (isJumping && jumpInput == false)
			{
				isJumping = false;
			}

			if (isGrounded == false && isJumping == false)
			{
				isGrounded = true;
			}
			
			isDoubleJumping = false;
			isTouchingCeiling = false;
			groundedDelayCoroutineIsRunning = false;
		}
		else
		{
			if(isOnMovingPlatform == false)
			{
				if (groundedDelayCoroutineIsRunning == false)
				{
					StartCoroutine(SetIsGroundedWithDelay());
				}
			}
		}

		if(isTouchingWall && wallJumpDirection == movementDirection)
		{
			if (isWallSliding)
			{
				if (isPullingOffWall == false)
				{
					pullOffWallCoroutine = StartCoroutine(PullOffWall(variables.wallPullOffDelayTime));
				}
			}
			else
			{
				pullOffWallCoroutine = StartCoroutine(PullOffWall(0.0f));
			}
		}
		
		if (controller.isGrounded || isOnMovingPlatform)
		{
			if (isJumping == false)
			{
				appliedAcceleration = variables.groundAcceleration;
				appliedDeceleration = variables.groundDeceleration;
				appliedGravity = 0;
				appliedTerminalVelocity = variables.terminalVelocity;
			}
			
		}
		else
		{
			appliedAcceleration = variables.airAcceleration;
			appliedDeceleration = variables.airDeceleration;
			if (isWallSliding)
			{
				if (velocity.y < -0.5f)
				{
					if (isWallJumping == false)
					{
						appliedGravity = variables.gravity / variables.wallFriction;
						appliedTerminalVelocity = variables.terminalVelocity / variables.wallFriction;
					}
				}
				else
				{
					appliedGravity = variables.gravity;
					appliedTerminalVelocity = variables.terminalVelocity;
				}
			}
			else
			{
				appliedGravity = variables.gravity;
				appliedTerminalVelocity = variables.terminalVelocity;
			}

		}

		if (isTouchingRightWall || isTouchingLeftWall)
		{
			if(doubleJumpBufferCoroutine != null)
			{
				StopCoroutine (doubleJumpBufferCoroutine);
			}
			isTouchingCeiling = false;
			isTouchingWall = true;
			isDoubleJumping = false;
            if (isAiming == false)
            {
                movementInput = false;
            }

        }
		else
		{
			if(isPullingOffWall == false)
			{
				isTouchingWall = false;
			}
		}

		if (controller.collisionState.above)
		{
//			if(controller.isGrounded)
//			{
//				Debug.Log ("Player Crushed");
//			}

			velocity.y = -0.5f;
			jumpInputEndTime = variables.jumpInputMaxTime;

			if(jumpCoroutine != null)
			{
				StopCoroutine (jumpCoroutine);
			}

			if (doubleJumpCoroutine != null)
			{
				StopCoroutine (doubleJumpCoroutine);
			}

			if (wallJumpCoroutine != null)
			{
				StopCoroutine (wallJumpCoroutine);
			}

			isJumping = false;
			jumpCoroutine = null;
			isWallJumping = false;
			isDoubleJumping = false;
			isTouchingCeiling = true;



		}

		if (controller.isGrounded)
		{
			canWallJump = false;
			if(isWallSliding)
			{
				xDirection *= -1;
				isWallSliding = false;
				appliedSpeed = 0;
			}
		}

		if (controller.collisionState.right && isWallJumping == false)
		{
			wallJumpDirection = -1;

			if (stickToWall || isOnRightSideOfMovingPlatform)
			{
				if (movementDirection == 1)
				{
					isTouchingRightWall = true;
					appliedSpeed = 0.5f;
					controller.velocity.x = 0.5f;
				}
			}

		}
		else
		{
			isTouchingRightWall = false;
		}

		if (controller.collisionState.left && isWallJumping == false)
		{
			wallJumpDirection = 1;

			if (stickToWall || isOnLeftSideOfMovingPlatform && isPullingOffWall == false)
			{
				if (movementDirection == -1)
				{
					appliedSpeed = -0.5f;
					controller.velocity.x = -0.5f;
					isTouchingLeftWall = true;
				}
			}
		}
		else
		{	
			isTouchingLeftWall = false;
		}


		velocity.x = appliedSpeed;

		 //Y Speed Part 1//


		if (isJumping == false)
		{
			if (isOnMovingPlatform)
			{
				controller.velocity.y = -10f;
			}

			else if (velocity.y + appliedGravity * Time.deltaTime / 2 < -appliedTerminalVelocity)
			{
				velocity.y = -appliedTerminalVelocity;
			}

			else if (controller.isGrounded)
			{
				controller.velocity.y = -0.1f;
			}

			else
			{
				if (UseGravity)
				{
					velocity.y += (appliedGravity * Time.deltaTime) / 2;
				}
			}
		}
		else
		{
			if (UseGravity)
			{
				velocity.y += (appliedGravity * Time.deltaTime) / 2;
			}
		}

		if(variables.controlsEnabled)
		{
			combinedForces = Vector3.zero;
			combinedForces = velocity + movingPlatformVelocity;
			combinedForces.x *= variables.hitModifier;
            if(Mathf.Abs(combinedForces.y) > appliedTerminalVelocity)
            {
                if(combinedForces.y > 0.0f)
                {
                    combinedForces.y = appliedTerminalVelocity;
                }
                else if (combinedForces.y < 0.0f)
                {
                    combinedForces.y = -appliedTerminalVelocity;
                }
            }
			
			controller.move( (combinedForces) * Time.deltaTime );
		}

		if (isJumping == false)
		{
			if (isOnMovingPlatform)
			{
				controller.velocity.y = -10f;
			}

			else if (velocity.y + appliedGravity * Time.deltaTime / 2 < -appliedTerminalVelocity)
			{
				velocity.y = -appliedTerminalVelocity;
			}

			else if (controller.isGrounded)
			{
				controller.velocity.y = -0.1f;
			}
			else
			{
				if (UseGravity)
				{
					velocity.y += (appliedGravity * Time.deltaTime) / 2;
				}
			}
		}
		else
		{
			if (UseGravity)
			{
				velocity.y += (appliedGravity * Time.deltaTime) / 2;
			}
		}








		// grab our current velocity to use as a base for all calculations
		velocity = controller.velocity;
	}

	void FixedUpdate()
	{
//		rbVelocity = (rb.position - rbPositionLastFrame) / Time.deltaTime;
//		rbPositionLastFrame = rb.pox	}
//		rbPositionLastFrame = rb	}
	}

	IEnumerator SetIsGroundedWithDelay()
	{
		groundedDelayCoroutineIsRunning = true;
		yield return new WaitForSeconds(variables.timeToRemainGroundedInAir);
		isGrounded = false;
	}

	IEnumerator Jump()
	{
		if(jumpInputBufferingCoroutine != null)
		{
			StopCoroutine (jumpInputBufferingCoroutine);
		}

		isJumping = true;
		_yPosOnJumpStart = tf.position.y;
		isGrounded = false;
		jumpInputTimeHeld = 0.0f;
		jumpInputEndTime = variables.jumpInputMinTime;
		controller.velocity.y = 0;
		velocity.y = 0;
		isOnMovingPlatform = false;
		movingPlatformVelocity = Vector3.zero;

		while (jumpInputTimeHeld < jumpInputEndTime && isTouchingCeiling == false)
		{
			if (jumpInputEndTime == variables.jumpInputMaxTime)
			{
				if (jumpInput == true)
				{
				}
				else
				{
					jumpInputTimeHeld = jumpInputEndTime;
				}

			}

			if (jumpInputTimeHeld + Time.deltaTime > jumpInputEndTime)
			{
				if (jumpInput == true) 
				{
					jumpInputEndTime = variables.jumpInputMaxTime;
				} 
			}

			if (controller.velocity.y + variables.jumpStrength*Time.deltaTime > variables.maxJumpVelocity)
			{
				controller.velocity.y = variables.maxJumpVelocity;
				velocity.y = variables.maxJumpVelocity;
			}
			else
			{
				velocity.y += variables.jumpStrength*Time.deltaTime;
			}
			jumpInputTimeHeld += Time.deltaTime;
			yield return new WaitForEndOfFrame();
		}

		//isJumping = false;
		jumpCoroutine = null;


	}

	IEnumerator DoubleJump()
	{

		isJumping = true;
		isDoubleJumping = true;
		isGrounded = false;
		jumpInputTimeHeld = 0.0f;
		jumpInputEndTime = variables.jumpInputMinTime;

		while (jumpInputTimeHeld < jumpInputEndTime && isTouchingCeiling == false)
		{


			if (jumpInputEndTime == variables.jumpInputMaxTime)
			{
				if (jumpInput == true)
				{
				}
				else
				{
					jumpInputTimeHeld = jumpInputEndTime;
				}

			}

			if (jumpInputTimeHeld + Time.deltaTime > jumpInputEndTime)
			{
				if (jumpInput == true)
				{
					jumpInputEndTime = variables.jumpInputMaxTime;
				}
			}

			jumpInputTimeHeld += Time.deltaTime;
			velocity.y = variables.jumpStrength;
			yield return new WaitForEndOfFrame();
		}
			
	}

	IEnumerator WallJump()
	{
/*		if (isTouchingLeftWall)
		{
			Debug.LogError("Wall Jump");
		}*/
		xDirection = wallJumpDirection;
		targetSpeed = movementSpeed * xDirection;
		var wallJjumpMaxXVelocity = targetSpeed;
		controller.velocity.y = 0.0f;
		controller.velocity.x = 0.0f;
		appliedSpeed = 0.0f;
		velocity.y = 0.0f;
		movementDirection = wallJumpDirection;
		isWallJumping = true;
		canWallJump = false;
		isWallSliding = false;
		isGrounded = false;
		isTouchingWall = false;
		jumpInputTimeHeld = 0.0f;
		jumpInputEndTime = variables.wallJumpInputMinTime;

		if (pullOffWallCoroutine != null)
		{
			StopCoroutine(pullOffWallCoroutine);
			isPullingOffWall = false;
		}


		while (jumpInputTimeHeld < jumpInputEndTime)
		{
			if (jumpInputEndTime == variables.wallJumpInputMaxTime)
			{
				if (jumpInput)
				{
				}
				else
				{
					jumpInputTimeHeld = jumpInputEndTime;
				}

			}

			if (jumpInputTimeHeld + 1 * Time.deltaTime > jumpInputEndTime)
			{
				if (jumpInput)
				{
					jumpInputEndTime = variables.wallJumpInputMaxTime;
				}
			}

			jumpInputTimeHeld +=  Time.deltaTime;
			appliedWallJumpStrength = variables.wallJumpXStrength*wallJumpDirection;
			
			if (controller.velocity.y + variables.jumpStrength*Time.deltaTime > variables.maxWallJumpVelocity)
			{
				controller.velocity.y = variables.maxWallJumpVelocity;
				velocity.y = variables.maxWallJumpVelocity;
			}
			else
			{
				velocity.y += variables.wallJumpYStrength*Time.deltaTime;
			}

			if (Mathf.Abs(controller.velocity.x + appliedWallJumpStrength*Time.deltaTime) > wallJjumpMaxXVelocity)
			{
				appliedSpeed = wallJjumpMaxXVelocity;
			}
			else
			{
				appliedSpeed += appliedWallJumpStrength*Time.deltaTime;
			}

			yield return new WaitForEndOfFrame();
		}
		
		isWallJumping = false;
	}


	private void MovementSpeedUpdate()
	{
		if (variables.movementEnabled)
		{
			if (isCrouching)
			{
				movementSpeed = variables.crouchSpeed;
			}
			else if (isAiming)
			{
				if (isGrounded)
				{
					movementSpeed = 0;
				}
				else
				{
					if(movementSpeed == 0)
					{
						movementSpeed = 0;
					}
				}
			}
			else
			{
				if (variables.movementSpeedType == PlayerMovement.PlayerMovementVariables.MovementSpeedType.Walk)
				{
					movementSpeed = variables.walkSpeed;
				}
				else if (variables.movementSpeedType == PlayerMovement.PlayerMovementVariables.MovementSpeedType.Jog)
				{
					movementSpeed = variables.jogSpeed;
				}
				else if (variables.movementSpeedType == PlayerMovement.PlayerMovementVariables.MovementSpeedType.Run)
				{
					movementSpeed = variables.runSpeed;
				}
			}

			if(isTouchingWall == false)
			{
				if (movementInput)
				{
					targetSpeed = movementSpeed * xDirection;
	
						if (xDirection == 1)
						{
							if (appliedSpeed + (appliedAcceleration * Time.deltaTime) > targetSpeed)
							{
								appliedSpeed = targetSpeed;
							}
							else
							{
								appliedSpeed += appliedAcceleration * Time.deltaTime;
							}
	
						}
						else if (xDirection == -1)
						{
	
							if (appliedSpeed - (appliedAcceleration * Time.deltaTime) < targetSpeed)
							{
								appliedSpeed = targetSpeed;
							}
							else
							{
								appliedSpeed -= appliedAcceleration * Time.deltaTime;
							}
						}
				}
				else
				{
					if (isWallJumping)
					{
						
					}
					else
					{
						if (appliedSpeed > 0)
						{
							if (appliedSpeed - appliedDeceleration * Time.deltaTime < 0)
							{
								appliedSpeed = 0;
							}
							else
							{
								appliedSpeed -= appliedDeceleration * Time.deltaTime;
							}
						}
						else if (appliedSpeed < 0)
						{
							if (appliedSpeed + appliedDeceleration * Time.deltaTime > 0)
							{
								appliedSpeed = 0;
							}
							else
							{
								appliedSpeed += appliedDeceleration * Time.deltaTime;
							}
						}
					}
				}
			}
		}

	}

	IEnumerator PullOffWall(float wallPullOffTime)
	{
		isPullingOffWall = true;
		stickToWall = false;
		yield return new WaitForSeconds(wallPullOffTime);
		xDirection = movementDirection;
		appliedSpeed = 0;
		velocity.x = 0;
		isWallSliding = false;
		isTouchingWall = false;
		isTouchingLeftWall = false;
		isTouchingRightWall = false;
		isPullingOffWall = false;
		stickToWall = true;
	}


	bool IsFalling()
	{
		if (isGrounded)
		{
			return false;
		}
		else
		{
			if (isJumping)
			{

				if (hasReachedJumpApex)
				{
					return true;
				}
				else
				{
					return false;
				}
			}
			else
			{
				return true;
			}
		}

	}

	bool IsFallingLongDistance()
	{
		if (isFalling)
		{

			if ((fallTime += Time.deltaTime) > variables.timeToFallIsLongDistance)
			{
				return true;
			}
			else
			{
				fallTime += Time.deltaTime;
				return false;
			}
		}
		else
		{
			return false;
		}
	}

	void AnimationMessageHandling()
	{
		if(controller.isGrounded)
		{
			if(isTouchingWall)
			{
				if (isCrouching)
				{
					if (movementInput)
					{
						if(lastMessageSent != "OnCrouch"+xDirection)
						{
							Messenger.Publish (new OnCrouch(xDirection));
							lastMessageSent = "OnCrouch"+xDirection;
						}
					}
					else
					{
						if (lastMessageSent != "OnCrouch"+xDirection)
						{
							Messenger.Publish (new OnCrouch (xDirection));
							lastMessageSent = "OnCrouch"+xDirection;
						}

					}
				}
				else
				{
					if (lastMessageSent != "OnIdle"+xDirection)
					{
						Messenger.Publish (new OnIdle (xDirection));
						lastMessageSent = "OnIdle"+xDirection;
					}
/*					if (movementInput)
					{

							if (lastMessageSent != "OnIdle"+xDirection)
							{
								Messenger.Publish (new OnIdle (xDirection));
								lastMessageSent = "OnIdle"+xDirection;
							}	
					}
					else
					{
						if (lastMessageSent != "OnTouchingWall"+xDirection)
						{
							Messenger.Publish (new OnTouchingWall (xDirection));
							lastMessageSent = "OnTouchingWall"+xDirection;
						}
					}*/					
				}

			}
			else
			{
				if (isCrouching)
				{
					if (movementInput)
					{
						if (lastMessageSent != "OnCrouchMovement"+xDirection)
						{
							Messenger.Publish (new OnCrouchMovement (xDirection));
							lastMessageSent = "OnCrouchMovement"+xDirection;
						}
					}
					else
					{
						if (lastMessageSent != "OnCrouch"+xDirection)
						{
							Messenger.Publish (new OnCrouch (xDirection));
							lastMessageSent = "OnCrouch"+xDirection;
						}
					}
				}
				else
				{
					if (movementInput && isAiming == false && variables.movementEnabled)
					{
						if (lastMessageSent != "OnGroundedMovement"+xDirection)
						{
							Messenger.Publish (new OnGroundedMovement (xDirection, variables.movementSpeedType.ToString ()));
							lastMessageSent = "OnGroundedMovement"+xDirection;
						}
					}
					else
					{
						if (lastMessageSent != "OnIdle"+xDirection)
						{
							Messenger.Publish (new OnIdle (xDirection));
							lastMessageSent = "OnIdle"+xDirection;
						}
					}					
				}
			}
		}
		else
		{
			if (isTouchingWall)
			{

				if (isWallSliding)
				{
					if (lastMessageSent != "OnWallSlide" + xDirection)
					{
						Messenger.Publish (new OnWallSlide (xDirection));
						lastMessageSent = "OnWallSlide" + xDirection;
					}
				}
				else
				{
					if (lastMessageSent != "OnJump"+xDirection)
					{
						
						Messenger.Publish (new OnJump (xDirection));
						lastMessageSent = "OnJump"+xDirection;
					}
				}

			}
			else
			{
				if(variables.jumpEnabled)
				{
					if (isJumping)
					{
						if (lastMessageSent != "OnJump"+xDirection)
						{
							Messenger.Publish (new OnJump (xDirection));
							lastMessageSent = "OnJump"+xDirection;
						}
					}
					else if (isWallJumping)
					{
						if (lastMessageSent != "OnWallJump")
						{
							Messenger.Publish (new OnWallJump (xDirection));
							lastMessageSent = "OnWallJump";
						}
					}
					else if (isDoubleJumping)
					{
						if (lastMessageSent != "OnDoubleJump"+xDirection)
						{
							Messenger.Publish (new OnDoubleJump (xDirection));
							lastMessageSent = "OnDoubleJump"+xDirection;
						}
					}
					else
					{
						if (lastMessageSent != "OnFalling"+xDirection)
						{
							Messenger.Publish (new OnFalling (xDirection));
							lastMessageSent = "OnFalling"+xDirection;
						}
					}
				}
			}
		}

	}


	public void LoadSettings()
	{
		
		if (characterType == CharacterType.Discman)
		{
			variables.movementSpeedType = discmanMovementSettiings.variables.movementSpeedType;
			variables.runSpeed = discmanMovementSettiings.variables.runSpeed;
			variables.walkSpeed = discmanMovementSettiings.variables.walkSpeed;
			variables.jogSpeed = discmanMovementSettiings.variables.jogSpeed;
			variables.crouchSpeed = discmanMovementSettiings.variables.crouchSpeed;
			variables.groundAcceleration = discmanMovementSettiings.variables.groundAcceleration;
			variables.groundDeceleration = discmanMovementSettiings.variables.groundDeceleration;
			variables.airAcceleration = discmanMovementSettiings.variables.airAcceleration;
			variables.airDeceleration = discmanMovementSettiings.variables.airDeceleration;
			variables.gravity = discmanMovementSettiings.variables.gravity;
			variables.terminalVelocity = discmanMovementSettiings.variables.terminalVelocity;
			variables.jumpStrength = discmanMovementSettiings.variables.jumpStrength;
			variables.maxJumpVelocity = discmanMovementSettiings.variables.maxJumpVelocity;
			variables.jumpInputMinTime = discmanMovementSettiings.variables.jumpInputMinTime;
			variables.jumpInputMaxTime = discmanMovementSettiings.variables.jumpInputMaxTime;
			variables.wallCheckContactFilter = discmanMovementSettiings.variables.wallCheckContactFilter;
			variables.wallJumpInputMinTime = discmanMovementSettiings.variables.wallJumpInputMinTime;
			variables.wallJumpInputMaxTime = discmanMovementSettiings.variables.wallJumpInputMaxTime;
			variables.wallJumpXStrength = discmanMovementSettiings.variables.wallJumpXStrength;
			variables.wallJumpYStrength = discmanMovementSettiings.variables.wallJumpYStrength;
			variables.maxWallJumpVelocity = discmanMovementSettiings.variables.maxWallJumpVelocity;
			variables.wallPullOffDelayTime = discmanMovementSettiings.variables.wallPullOffDelayTime;
			variables.wallSlideDelayTime = discmanMovementSettiings.variables.wallSlideDelayTime;
			variables.wallSlideReleaseDelayTime = discmanMovementSettiings.variables.wallSlideReleaseDelayTime;
			variables.wallFriction = discmanMovementSettiings.variables.wallFriction;
			variables.timeToRemainGroundedInAir = discmanMovementSettiings.variables.timeToRemainGroundedInAir;
			variables.timeToFallIsLongDistance = discmanMovementSettiings.variables.timeToFallIsLongDistance;
			variables.controlsEnabled = discmanMovementSettiings.variables.controlsEnabled;
			variables.movementEnabled = discmanMovementSettiings.variables.movementEnabled;
			variables.doubleJumpEnabled = discmanMovementSettiings.variables.doubleJumpEnabled;
			variables.jumpEnabled = discmanMovementSettiings.variables.jumpEnabled;
			variables.wallJumpEnabled = discmanMovementSettiings.variables.wallJumpEnabled;
            variables.wallSlideEnabled = discmanMovementSettiings.variables.wallSlideEnabled;
            variables.crouchingEnabled = discmanMovementSettiings.variables.crouchingEnabled;
			variables.groundSlidingEnabled = discmanMovementSettiings.variables.groundSlidingEnabled;
			variables.jumpInputBufferTime = discmanMovementSettiings.variables.jumpInputBufferTime;
			variables.doubleJumpInpuBufferTime = discmanMovementSettiings.variables.doubleJumpInpuBufferTime;
			variables.crouchSize = discmanMovementSettiings.variables.crouchSize;	
			variables.useMidiControllerForTuning = discmanMovementSettiings.variables.useMidiControllerForTuning;
			variables.hitModifier = discmanMovementSettiings.variables.hitModifier;
		}
		else if (characterType == CharacterType.Danny)
		{
			variables.movementSpeedType = dannyMovementSettings.variables.movementSpeedType;
			variables.runSpeed = dannyMovementSettings.variables.runSpeed;
			variables.walkSpeed = dannyMovementSettings.variables.walkSpeed;
			variables.jogSpeed = dannyMovementSettings.variables.jogSpeed;
			variables.crouchSpeed = dannyMovementSettings.variables.crouchSpeed;
			variables.groundAcceleration = dannyMovementSettings.variables.groundAcceleration;
			variables.groundDeceleration = dannyMovementSettings.variables.groundDeceleration;
			variables.airAcceleration = dannyMovementSettings.variables.airAcceleration;
			variables.airDeceleration = dannyMovementSettings.variables.airDeceleration;
			variables.gravity = dannyMovementSettings.variables.gravity;
			variables.terminalVelocity = dannyMovementSettings.variables.terminalVelocity;
			variables.jumpStrength = dannyMovementSettings.variables.jumpStrength;
			variables.maxJumpVelocity = dannyMovementSettings.variables.maxJumpVelocity;
			variables.jumpInputMinTime = dannyMovementSettings.variables.jumpInputMinTime;
			variables.jumpInputMaxTime = dannyMovementSettings.variables.jumpInputMaxTime;
			variables.wallCheckContactFilter = dannyMovementSettings.variables.wallCheckContactFilter;
			variables.wallJumpInputMinTime = dannyMovementSettings.variables.wallJumpInputMinTime;
			variables.wallJumpInputMaxTime = dannyMovementSettings.variables.wallJumpInputMaxTime;
			variables.wallJumpXStrength = dannyMovementSettings.variables.wallJumpXStrength;
			variables.wallJumpYStrength = dannyMovementSettings.variables.wallJumpYStrength;
			variables.maxWallJumpVelocity = dannyMovementSettings.variables.maxWallJumpVelocity;
			variables.wallPullOffDelayTime = dannyMovementSettings.variables.wallPullOffDelayTime;
			variables.wallSlideDelayTime = dannyMovementSettings.variables.wallSlideDelayTime;
			variables.wallSlideReleaseDelayTime = dannyMovementSettings.variables.wallSlideReleaseDelayTime;
			variables.wallFriction = dannyMovementSettings.variables.wallFriction;
			variables.timeToRemainGroundedInAir = dannyMovementSettings.variables.timeToRemainGroundedInAir;
			variables.timeToFallIsLongDistance = dannyMovementSettings.variables.timeToFallIsLongDistance;
			variables.controlsEnabled = dannyMovementSettings.variables.controlsEnabled;
			variables.movementEnabled = dannyMovementSettings.variables.movementEnabled;
			variables.doubleJumpEnabled = dannyMovementSettings.variables.doubleJumpEnabled;
			variables.jumpEnabled = dannyMovementSettings.variables.jumpEnabled;
			variables.wallJumpEnabled = dannyMovementSettings.variables.wallJumpEnabled;
            variables.wallSlideEnabled = dannyMovementSettings.variables.wallSlideEnabled;
            variables.crouchingEnabled = dannyMovementSettings.variables.crouchingEnabled;
			variables.groundSlidingEnabled = dannyMovementSettings.variables.groundSlidingEnabled;
			variables.jumpInputBufferTime = dannyMovementSettings.variables.jumpInputBufferTime;
			variables.doubleJumpInpuBufferTime = dannyMovementSettings.variables.doubleJumpInpuBufferTime;
			variables.crouchSize = dannyMovementSettings.variables.crouchSize;	
			variables.useMidiControllerForTuning = dannyMovementSettings.variables.useMidiControllerForTuning;
			variables.hitModifier = dannyMovementSettings.variables.hitModifier;
		}
	}

	public void SaveSettings()
	{
		if (characterType == CharacterType.Discman)
		{
			discmanMovementSettiings.variables.movementSpeedType = variables.movementSpeedType;
			discmanMovementSettiings.variables.runSpeed = variables.runSpeed;
			discmanMovementSettiings.variables.walkSpeed = variables.walkSpeed;
			discmanMovementSettiings.variables.jogSpeed = variables.jogSpeed;
			discmanMovementSettiings.variables.crouchSpeed = variables.crouchSpeed;
			discmanMovementSettiings.variables.groundAcceleration = variables.groundAcceleration;
			discmanMovementSettiings.variables.groundDeceleration = variables.groundDeceleration;
			discmanMovementSettiings.variables.airAcceleration = variables.airAcceleration;
			discmanMovementSettiings.variables.airDeceleration = variables.airDeceleration;
			discmanMovementSettiings.variables.gravity = variables.gravity;
			discmanMovementSettiings.variables.terminalVelocity = variables.terminalVelocity;
			discmanMovementSettiings.variables.jumpStrength = variables.jumpStrength;
			discmanMovementSettiings.variables.maxJumpVelocity = variables.maxJumpVelocity;
			discmanMovementSettiings.variables.jumpInputMinTime = variables.jumpInputMinTime;
			discmanMovementSettiings.variables.jumpInputMaxTime = variables.jumpInputMaxTime;
			discmanMovementSettiings.variables.wallCheckContactFilter = variables.wallCheckContactFilter;
			discmanMovementSettiings.variables.wallJumpInputMinTime = variables.wallJumpInputMinTime;
			discmanMovementSettiings.variables.wallJumpInputMaxTime = variables.wallJumpInputMaxTime;
			discmanMovementSettiings.variables.wallJumpXStrength = variables.wallJumpXStrength;
			discmanMovementSettiings.variables.wallJumpYStrength = variables.wallJumpYStrength;
			discmanMovementSettiings.variables.maxWallJumpVelocity = variables.maxWallJumpVelocity;
			discmanMovementSettiings.variables.wallPullOffDelayTime = variables.wallPullOffDelayTime;
			discmanMovementSettiings.variables.wallSlideDelayTime = variables.wallSlideDelayTime;
			discmanMovementSettiings.variables.wallSlideReleaseDelayTime = variables.wallSlideReleaseDelayTime;
			discmanMovementSettiings.variables.wallFriction = variables.wallFriction;
			discmanMovementSettiings.variables.timeToRemainGroundedInAir = variables.timeToRemainGroundedInAir;
			discmanMovementSettiings.variables.timeToFallIsLongDistance = variables.timeToFallIsLongDistance;
			discmanMovementSettiings.variables.controlsEnabled = variables.controlsEnabled;
			discmanMovementSettiings.variables.movementEnabled = variables.movementEnabled;
			discmanMovementSettiings.variables.doubleJumpEnabled = variables.doubleJumpEnabled;
			discmanMovementSettiings.variables.jumpEnabled = variables.jumpEnabled;
			discmanMovementSettiings.variables.wallJumpEnabled = variables.wallJumpEnabled;
            discmanMovementSettiings.variables.wallSlideEnabled = variables.wallSlideEnabled;
            discmanMovementSettiings.variables.crouchingEnabled = variables.crouchingEnabled;
			discmanMovementSettiings.variables.groundSlidingEnabled = variables.groundSlidingEnabled;
			discmanMovementSettiings.variables.jumpInputBufferTime = variables.jumpInputBufferTime;
			discmanMovementSettiings.variables.doubleJumpInpuBufferTime = variables.doubleJumpInpuBufferTime;
			discmanMovementSettiings.variables.crouchSize = variables.crouchSize;	
			discmanMovementSettiings.variables.useMidiControllerForTuning = variables.useMidiControllerForTuning;
			discmanMovementSettiings.variables.hitModifier = variables.hitModifier;
		}
		else if (characterType == CharacterType.Danny)
		{
			dannyMovementSettings.variables.movementSpeedType = variables.movementSpeedType;
			dannyMovementSettings.variables.runSpeed = variables.runSpeed;
			dannyMovementSettings.variables.walkSpeed = variables.walkSpeed;
			dannyMovementSettings.variables.jogSpeed = variables.jogSpeed;
			dannyMovementSettings.variables.crouchSpeed = variables.crouchSpeed;
			dannyMovementSettings.variables.groundAcceleration = variables.groundAcceleration;
			dannyMovementSettings.variables.groundDeceleration = variables.groundDeceleration;
			dannyMovementSettings.variables.airAcceleration = variables.airAcceleration;
			dannyMovementSettings.variables.airDeceleration = variables.airDeceleration;
			dannyMovementSettings.variables.gravity = variables.gravity;
			dannyMovementSettings.variables.terminalVelocity = variables.terminalVelocity;
			dannyMovementSettings.variables.jumpStrength = variables.jumpStrength;
			dannyMovementSettings.variables.maxJumpVelocity = variables.maxJumpVelocity;
			dannyMovementSettings.variables.jumpInputMinTime = variables.jumpInputMinTime;
			dannyMovementSettings.variables.jumpInputMaxTime = variables.jumpInputMaxTime;
			dannyMovementSettings.variables.wallCheckContactFilter = variables.wallCheckContactFilter;
			dannyMovementSettings.variables.wallJumpInputMinTime = variables.wallJumpInputMinTime;
			dannyMovementSettings.variables.wallJumpInputMaxTime = variables.wallJumpInputMaxTime;
			dannyMovementSettings.variables.wallJumpXStrength = variables.wallJumpXStrength;
			dannyMovementSettings.variables.wallJumpYStrength = variables.wallJumpYStrength;
			dannyMovementSettings.variables.maxWallJumpVelocity = variables.maxJumpVelocity;
			dannyMovementSettings.variables.wallPullOffDelayTime = variables.wallPullOffDelayTime;
			dannyMovementSettings.variables.wallSlideDelayTime = variables.wallSlideDelayTime;
			dannyMovementSettings.variables.wallSlideReleaseDelayTime = variables.wallSlideReleaseDelayTime;
			dannyMovementSettings.variables.wallFriction = variables.wallFriction;
			dannyMovementSettings.variables.timeToRemainGroundedInAir = variables.timeToRemainGroundedInAir;
			dannyMovementSettings.variables.timeToFallIsLongDistance = variables.timeToFallIsLongDistance;
			dannyMovementSettings.variables.controlsEnabled = variables.controlsEnabled;
			dannyMovementSettings.variables.movementEnabled = variables.movementEnabled;
			dannyMovementSettings.variables.doubleJumpEnabled = variables.doubleJumpEnabled;
			dannyMovementSettings.variables.jumpEnabled = variables.jumpEnabled;
			dannyMovementSettings.variables.wallJumpEnabled = variables.wallJumpEnabled;
            dannyMovementSettings.variables.wallSlideEnabled = variables.wallSlideEnabled;
            dannyMovementSettings.variables.crouchingEnabled = variables.crouchingEnabled;
			dannyMovementSettings.variables.groundSlidingEnabled = variables.groundSlidingEnabled;
			dannyMovementSettings.variables.jumpInputBufferTime = variables.jumpInputBufferTime;
			dannyMovementSettings.variables.doubleJumpInpuBufferTime = variables.doubleJumpInpuBufferTime;
			dannyMovementSettings.variables.crouchSize = variables.crouchSize;	
			dannyMovementSettings.variables.useMidiControllerForTuning = variables.useMidiControllerForTuning;
			dannyMovementSettings.variables.hitModifier = variables.hitModifier;
		}
	}

	public void CheckIfCanWallJump()
	{
		RaycastHit2D[] wallCheckHit = new RaycastHit2D[1];
		if (Physics2D.Raycast (tf.position, new Vector2 (xDirection, 0), variables.wallCheckContactFilter, wallCheckHit, (velocity.x * Time.deltaTime + widthOffset)+0.1f) > 0)
		{
			if (isGrounded == false && isWallJumping == false)
			{
				canWallJump = true;
				//wallJumpDirection = xDirection * -1;
				if (movementInput && movementDirection != wallJumpDirection && variables.wallSlideEnabled)
				{
					wallDetectedTime += Time.deltaTime;
					if (wallDetectedTime >= variables.wallSlideDelayTime)
					{
						isWallSliding = true;
					}
				}
			}
			else
			{
				wallDetectedTime = 0;
			}


		}
		else
		{
			wallDetectedTime = 0.0f;
			isWallSliding = false;
			canWallJump = false;
		}
	}

	public IEnumerator EaseOutOfWallSlide()
	{
		yield return new WaitForSeconds (variables.wallSlideReleaseDelayTime);
		isWallSliding = false;
	}
	public void SwitchCharacterType(string character)
	{
		if (character == "Discman")
		{
			characterType = CharacterType.Discman;
		}
		else if (character == "Danny")
		{
			characterType = CharacterType.Danny;
		}

		LoadSettings ();
	}

	void OnApplicationQuit()
	{
		discmanMovementSettiings.variables.movementEnabled = true;
		discmanMovementSettiings.variables.jumpEnabled = true;
		dannyMovementSettings.variables.movementEnabled = true;
		dannyMovementSettings.variables.jumpEnabled = true;
	}

	void OnPlayerDied()
	{
		DisableControls ();
		Messenger.Publish (new OnPlayerDied());
	}

	[System.Serializable]
	public class PlayerMovementVariables
	{
		public enum MovementSpeedType {Walk, Jog, Run}
		public MovementSpeedType movementSpeedType;
		public float runSpeed = 0.0f;
		public float walkSpeed = 0.0f;
		public float jogSpeed = 0.0f;
		public float crouchSpeed = 0.0f;
		public float groundAcceleration = 0.0f;
		public float groundDeceleration = 0.0f;
		public float airAcceleration = 0.0f;
		public float airDeceleration = 0.0f;
		public float gravity = 0.0f;
		public float terminalVelocity = 0.0f;
		public float jumpStrength = 0.0f;
		public float maxJumpVelocity = 0.0f;
		public float jumpInputMinTime = 0.05f;
		public float jumpInputMaxTime = 0.0f;
		public ContactFilter2D wallCheckContactFilter;
		public float wallJumpInputMinTime = 0.05f;
		public float wallJumpInputMaxTime = 0.0f;
		public float wallJumpXStrength = 0.0f;
		public float wallJumpYStrength = 0.0f;
		public float maxWallJumpVelocity = 0.0f;
		public float wallPullOffDelayTime = 0.0f;
		public float wallSlideDelayTime = 0.125f;
		public float wallSlideReleaseDelayTime = 0.9f;
		public float wallFriction = 0.0f;
		public float timeToRemainGroundedInAir = 0.0f;
		public float timeToFallIsLongDistance = 0.0f;
		public bool controlsEnabled = true;
		public bool movementEnabled = true;
		public bool doubleJumpEnabled = false;
		public bool jumpEnabled = false;
		public bool wallJumpEnabled = false;
        public bool wallSlideEnabled = false;
		public bool crouchingEnabled = false;
		public bool groundSlidingEnabled = false;
		public float jumpInputBufferTime = 0.0f;
		public float doubleJumpInpuBufferTime = 0.0f;
		public float crouchSize = 0.5f;	
		public bool useMidiControllerForTuning = false;
		public float hitModifier = 1.0f;
	}



}


