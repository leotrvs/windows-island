using System;

namespace WindowsIsland
{
    // A damped spring, stepped once per rendered frame. This is what gives the island its iOS-like
    // motion: shape and content keep their velocity when the target changes halfway through.
    sealed class Spring
    {
        public double Value;
        public double Velocity;
        public double Target;

        double stiffness;
        double damping;
        readonly double epsilon;

        public Spring(double value, double epsilon = 0.05, double stiffness = 300, double dampingRatio = 0.8)
        {
            Value = Target = value;
            this.epsilon = epsilon;
            Tune(stiffness, dampingRatio);
        }

        public bool Settled
        {
            get { return Math.Abs(Value - Target) < epsilon && Math.Abs(Velocity) < epsilon * 10; }
        }

        // dampingRatio below 1 overshoots a little (bouncy); 1 settles as fast as possible without overshoot.
        public void Tune(double stiffness, double dampingRatio)
        {
            this.stiffness = stiffness;
            damping = 2 * dampingRatio * Math.Sqrt(stiffness);
        }

        public void Snap(double value)
        {
            Value = Target = value;
            Velocity = 0;
        }

        // Returns true while the spring is still moving.
        public bool Step(double dt)
        {
            if (Settled)
            {
                Value = Target;
                Velocity = 0;
                return false;
            }

            int steps = Math.Max(1, (int)Math.Ceiling(dt * 480));
            double h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                double acceleration = -stiffness * (Value - Target) - damping * Velocity;
                Velocity += acceleration * h;
                Value += Velocity * h;
            }
            return true;
        }
    }
}
