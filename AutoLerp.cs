/*
 * Copyright (c) 2019 Tanvas, Inc.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without modification,
 * are permitted provided that the following conditions are met:
 *
 *  * Redistributions of source code must retain the above copyright notice, this
 * list of conditions and the following disclaimer.
 *  * Redistributions in binary form must reproduce the above copyright notice,
 * this list of conditions and the following disclaimer in the documentation and/or
 * other materials provided with the distribution.
 *  * Neither the name of Tanvas, Inc. nor the names of its contributors may be
 * used to endorse or promote products derived from this software without specific
 * prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
 * ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
 * ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace TanvasTouchHapticKnob
{
    /// <summary>
    /// The AutoLerp will interpolate a float value from one value to another, over a period of time. It uses a basic ease-out
    /// function, so the rate of change will decrease as the lerp nears completion.
    /// </summary>
    class AutoLerp
    {
        // Moving FROM
        private readonly float lerpFrom;

        // Moving TO
        private readonly float lerpTo;

        // The total duration of the slide.
        private readonly double durationMS;

        // Delegate of function to call with the updated percentage.
        public delegate void UpdatedValueDelegate(float value);

        // The actual function to call.
        private readonly UpdatedValueDelegate updatedValueDelegate;

        // The timer.
        private readonly DispatcherTimer dispatcherTimer;

        // The stopwatch for measuring the time.
        private readonly Stopwatch stopwatch;

        // Number of MS for the timer to fire.
        private const int TIMER_TICK_MS = 10;

        /// <summary>
        /// Main constructor - setup up the start/end and starts the timer. The callback will be called frequently
        /// while timing.
        /// </summary>
        /// <param name="_lerpFrom">Starting value (ie: 0.0f)</param>
        /// <param name="_lerpTo">Ending value (ie: 1.0f)</param>
        /// <param name="_durationMS">Total duration of the lerp.</param>
        /// <param name="_updatedValueDelegate">Callback to call as the value is updated.</param>
        public AutoLerp(float _lerpFrom, float _lerpTo, double _durationMS, UpdatedValueDelegate _updatedValueDelegate)
        {
            lerpFrom = _lerpFrom;
            lerpTo = _lerpTo;
            durationMS = _durationMS;
            updatedValueDelegate = _updatedValueDelegate;

            // Create the stopwatch.
            stopwatch = new Stopwatch();

            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += TimerTick;
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(TIMER_TICK_MS);

            // Now start the stopwatch and the timer.
            stopwatch.Start();
            dispatcherTimer.Start();
        }

        /// <summary>
        /// Called to cancel the lerp if you don't want to let it go all the way.
        /// </summary>
        public void Cancel()
        {
            stopwatch.Stop();
            dispatcherTimer.Stop();
        }

        /// <summary>
        /// Function called for every tick of the timer.
        /// </summary>
        private void TimerTick(object sender, EventArgs e)
        {
            // The eventual pct complete.
            float pctComplete = 0.0f;

            // How much time has passed.
            double elapsedMS = stopwatch.ElapsedMilliseconds;
            if (elapsedMS >= durationMS)
            {
                // Stop everything.
                Cancel();

                // We've completed the operation!
                pctComplete = 1.0f;
            }
            else
            {
                // In the middle.
                pctComplete = (float)(elapsedMS / durationMS);
            }

            // A nice ease-out function.
            pctComplete *= 2 - pctComplete;

            // How far into the slide..
            float range = lerpTo - lerpFrom;

            // The actual pct.
            float actualValue = lerpFrom + (range * pctComplete);

            // Call the function.
            updatedValueDelegate(actualValue);
        }

    }
}
