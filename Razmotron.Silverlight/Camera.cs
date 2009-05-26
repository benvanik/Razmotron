using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Razmotron
{
	public class Camera
	{
		public float NearZ = 0.1f;
		public float FarZ = 100.0f;

		public Matrix Projection;
		public Matrix View;
		public Matrix ViewProjection;

		public Vector3 Position;
		public Vector3 Angles; // yaw, pitch, roll

		public float Acceleration = 16.0f;
		public Vector3 PositionImpulse;
		public Vector3 PositionVelocity;
		public Vector3 AngularImpulse;
		public Vector3 AngularVelocity;

		private DateTime LastUpdate = DateTime.Now;

		public void SetPerspective( float fovY, float aspectRatio )
		{
			float f = 1.0f / ( float )Math.Tan( fovY / 2.0f );
			Projection = new Matrix(
				f * aspectRatio, 0, 0, 0,
				0, f, 0, 0,
				0, 0, FarZ / ( NearZ - FarZ ), 1 / 100.0f,
				0, 0, ( NearZ * FarZ ) / ( NearZ - FarZ ), 0 );
		}

		public void Calculate()
		{
			DateTime sample = DateTime.Now;
			float delta = ( float )( sample - LastUpdate ).TotalSeconds;
			LastUpdate = sample;

			Quaternion rotation;
			Quaternion.CreateFromYawPitchRoll( Angles.X, Angles.Y, Angles.Z, out rotation );
			Vector3 normal = PositionVelocity * delta;
			if( normal.LengthSquared != 0.0f )
				normal.Normalize();
			Vector3 ahead;
			Vector3.Transform( ref normal, ref rotation, out ahead );
			Vector3.Multiply( ref ahead, delta, out ahead );
			Position += ahead;
			Angles += ( AngularVelocity * delta );

			this.ApplyPhysics( delta );

			// NOTE: math is inverse!
			Matrix translation = Matrix.Identity;
			translation.M41 = -Position.X;
			translation.M42 = -Position.Y;
			translation.M43 = -Position.Z;
			Matrix rotx = Matrix.Identity;
			rotx.M22 = ( float )Math.Cos( Angles.Y );
			rotx.M23 = -( float )Math.Sin( Angles.Y );
			rotx.M32 = ( float )Math.Sin( Angles.Y );
			rotx.M33 = ( float )Math.Cos( Angles.Y );
			Matrix roty = Matrix.Identity;
			roty.M11 = ( float )Math.Cos( Angles.X );
			roty.M13 = ( float )Math.Sin( Angles.X );
			roty.M31 = -( float )Math.Sin( Angles.X );
			roty.M33 = ( float )Math.Cos( Angles.X );
			Matrix rotz = Matrix.Identity;
			rotz.M11 = ( float )Math.Cos( Angles.Z );
			rotz.M12 = -( float )Math.Sin( Angles.Z );
			rotz.M21 = ( float )Math.Sin( Angles.Z );
			rotz.M22 = ( float )Math.Cos( Angles.Z );

			// view = translation * rotx * roty * rotz
			Matrix temp;
			Matrix.Multiply( ref translation, ref roty, out View );
			Matrix.Multiply( ref View, ref rotx, out temp );
			Matrix.Multiply( ref temp, ref rotz, out View );

			Matrix.Multiply( ref View, ref Projection, out ViewProjection );
		}

		private void ApplyPhysics( float delta )
		{
			Vector3 direction = new Vector3( 1, 1, 1 );

			Vector3 velocityDelta;
			Vector3.Multiply( ref PositionImpulse, ( Acceleration * delta ), out velocityDelta );
			Vector3.Add( ref PositionVelocity, ref velocityDelta, out PositionVelocity );
			PositionVelocity = MathHelper.Clamp( PositionVelocity, new Vector3( -10, -10, -10 ), new Vector3( 10, 10, 10 ) );
			PositionVelocity = this.Decelerate( PositionImpulse, PositionVelocity, Acceleration, delta );

			Vector3.Multiply( ref AngularImpulse, ( Acceleration * delta ), out velocityDelta );
			Vector3.Add( ref AngularVelocity, ref velocityDelta, out AngularVelocity );
			AngularVelocity = MathHelper.Clamp( AngularVelocity, new Vector3( -3, -3, -3 ), new Vector3( 3, 3, 3 ) );
			AngularVelocity = this.Decelerate( AngularImpulse, AngularVelocity, Acceleration, delta );
		}

		private Vector3 Decelerate( Vector3 impulse, Vector3 velocity, float acceleration, float delta )
		{
			Vector3 newVelocity = velocity;
			if( impulse.X == 0.0f )
			{
				if( velocity.X > 0.0f )
				{
					newVelocity.X -= acceleration * delta;
					if( newVelocity.X < 0.0f ) newVelocity.X = 0.0f;
				}
				else
				{
					newVelocity.X += acceleration * delta;
					if( newVelocity.X > 0.0f ) newVelocity.X = 0.0f;
				}
			}
			if( impulse.Y == 0.0f )
			{
				if( velocity.Y > 0.0f )
				{
					newVelocity.Y -= acceleration * delta;
					if( newVelocity.Y < 0.0f ) newVelocity.Y = 0.0f;
				}
				else
				{
					newVelocity.Y += acceleration * delta;
					if( newVelocity.Y > 0.0f ) newVelocity.Y = 0.0f;
				}
			}
			if( impulse.Z == 0.0f )
			{
				if( velocity.Z > 0.0f )
				{
					newVelocity.Z -= acceleration * delta;
					if( newVelocity.Z < 0.0f ) newVelocity.Z = 0.0f;
				}
				else
				{
					newVelocity.Z += acceleration * delta;
					if( newVelocity.Z > 0.0f ) newVelocity.Z = 0.0f;
				}
			}
			return newVelocity;
		}

		public bool HandleKeyDown( Key key, int platformCode )
		{
			bool handled = false;
			switch( key )
			{
				case Key.A:
					PositionImpulse.X = 1.0f;
					handled = true;
					break;
				case Key.D:
					PositionImpulse.X = -1.0f;
					handled = true;
					break;
				case Key.S:
					PositionImpulse.Z = -1.0f;
					handled = true;
					break;
				case Key.W:
					PositionImpulse.Z = 1.0f;
					handled = true;
					break;
				case Key.Z:
					PositionImpulse.Y = -1.0f;
					handled = true;
					break;
				case Key.Q:
					PositionImpulse.Y = 1.0f;
					handled = true;
					break;

				case Key.Left:
					AngularImpulse.X = 1.0f;
					handled = true;
					break;
				case Key.Right:
					AngularImpulse.X = -1.0f;
					handled = true;
					break;
				case Key.Down:
					AngularImpulse.Y = 1.0f;
					handled = true;
					break;
				case Key.Up:
					AngularImpulse.Y = -1.0f;
					handled = true;
					break;
			}
			return handled;
		}

		public bool HandleKeyUp( Key key, int platformCode )
		{
			bool handled = false;
			switch( key )
			{
				case Key.A:
					PositionImpulse.X = 0.0f;
					handled = true;
					break;
				case Key.D:
					PositionImpulse.X = 0.0f;
					handled = true;
					break;
				case Key.S:
					PositionImpulse.Z = 0.0f;
					handled = true;
					break;
				case Key.W:
					PositionImpulse.Z = 0.0f;
					handled = true;
					break;
				case Key.Z:
					PositionImpulse.Y = 0.0f;
					handled = true;
					break;
				case Key.Q:
					PositionImpulse.Y = 0.0f;
					handled = true;
					break;

				case Key.Left:
					AngularImpulse.X = 0.0f;
					handled = true;
					break;
				case Key.Right:
					AngularImpulse.X = 0.0f;
					handled = true;
					break;
				case Key.Down:
					AngularImpulse.Y = 0.0f;
					handled = true;
					break;
				case Key.Up:
					AngularImpulse.Y = 0.0f;
					handled = true;
					break;
			}
			return handled;
		}
	}
}
