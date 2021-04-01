using System;
using System.Collections;
using System.Collections.Generic;


namespace FFVideoConverter
{
    public class TimeInterval
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public TimeSpan Duration => End - Start;

        public TimeInterval(TimeSpan start, TimeSpan end)
        {
            if (end >= start)
            {
                Start = start;
                End = end;
            }
            else
            {
                throw new Exception("Arguments not valid");
            }
        }

        /// <summary>
        /// Returns a value that determines if TimeSpan t is inside the TimeInterval
        /// </summary>
        public bool Contains(TimeSpan t)
        {
            return t >= Start && t <= End;
        }

        /// <summary>
        /// Returns a value that determines if one TimeInterval is completely inside this TimeInterval
        /// </summary>
        public bool Contains(TimeInterval timeInterval)
        {
            return timeInterval.Start >= Start && timeInterval.End <= End;
        }

        /// <summary>
        /// Returns a value that determines if two intervals share at least one point
        /// </summary>
        public bool Intersect(TimeInterval other)
        {
            //An intersection means that exist t so that Start <= t <= End && other.Start <= t <= other.End
            //This means that Start <= other.End and other.Start <= End
            return Start <= other.End && other.Start <= End; 
        }

        /// <summary>
        /// Joins two intervals that are adjacent
        /// </summary>
        public static TimeInterval operator +(TimeInterval t1, TimeInterval t2)
        {
            return t1.Add(t2);
        }

        /// <summary>
        /// Joins two intervals that are adjacent
        /// </summary>
        public TimeInterval Add(TimeInterval other)
        {
            if (Intersect(other))
                return new TimeInterval(Start < other.Start ? Start : other.Start, End > other.End ? End : other.End);
            else
                throw new Exception("Intervals do not intersect each other");
        }

        /// <summary>
        /// Removes an interval from another, if they are adjacent
        /// </summary>
        public static TimeInterval operator -(TimeInterval t1, TimeInterval t2)
        {
            return t1.Subtract(t2);
        }

        /// <summary>
        /// Removes an interval from another, provided it's not completely inside the interval
        /// </summary>
        public TimeInterval Subtract(TimeInterval other)
        {
            if (!Contains(other) && this != other)
            {
                if (!Intersect(other))
                {
                    return this;
                }
                if (this > other)
                {
                    return new TimeInterval(other.Start, End);
                }
                if (this < other)
                {
                    return new TimeInterval(Start, other.End);
                }
                return this; //this line will never run, but it's necessary for the compiler
            }
            else
            {
                throw new Exception("Interval can't be inside");
            }
        }

        public static bool operator <(TimeInterval t1, TimeInterval t2)
        {
            return t1.Start < t2.Start;
        }

        public static bool operator >(TimeInterval t1, TimeInterval t2)
        {
            return t1.Start > t2.Start;
        }
    }


    public class TimeIntervalCollection : IEnumerable<TimeInterval>
    {
        public TimeSpan TotalDuration
        {
            get
            {
                if (intervalList.Count == 0) return ActualEnd - ActualStart;

                TimeSpan totalDuration = TimeSpan.Zero;
                foreach (var item in intervalList)
                {
                    totalDuration += item.Duration;
                }
                return totalDuration;
            }
        }

        public int Count 
        {
            get 
            { 
                return intervalList.Count; 
            } 
        }

        /// <summary>
        /// Returns the start of the first TimeInterval, or Start if there are no TimeInterval elements
        /// </summary>
        public TimeSpan ActualStart
        {
            get
            {
                if (intervalList.Count > 0)
                    return intervalList[0].Start;
                return Start;
            }
        }

        /// <summary>
        /// Returns the end of the last TimeInterval, or End if there are no TimeInterval elements
        /// </summary>
        public TimeSpan ActualEnd
        {
            get
            {
                if (intervalList.Count > 0)
                    return intervalList[Count - 1].End;
                return End;
            }
        }

        public TimeSpan Start { get; private set; }
        public TimeSpan End { get; private set; }

        public TimeInterval this[int index]
        {
            get
            {
                if (index >= 0 && index < intervalList.Count)
                {
                    return intervalList[index];
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }
        }

        private readonly List<TimeInterval> intervalList;


        public TimeIntervalCollection(TimeSpan end) : this(TimeSpan.Zero, end)
        {
        }

        public TimeIntervalCollection(TimeSpan start, TimeSpan end)
        {
            intervalList = new List<TimeInterval>();
            Start = start;
            End = end;
        }

        /// <summary>
        /// Adds the time interval to this collection, ignoring the eventual section of the interval that is outside of the Start-End range
        /// </summary>
        public void Add(TimeSpan start, TimeSpan end)
        {
            Add(new TimeInterval(start, end));
        }

        /// <summary>
        /// Adds the timeInterval to this collection, ignoring the eventual section of the interval that is outside of the Start-End range
        /// </summary>
        public void Add(TimeInterval timeInterval)
        {
            TimeInterval collectionInterval = new TimeInterval(Start, End);
            if (collectionInterval.Intersect(timeInterval))
            {
                //Discards the part of the interval that's outisde of the collection range
                if (timeInterval.Start < Start)
                    timeInterval.Start = Start;
                if (timeInterval.End > End)
                    timeInterval.End = End;

                for (int i = intervalList.Count - 1; i >= 0; i--) //Loop is reversed to allow removing items from the list
                {
                    if (intervalList[i].Intersect(timeInterval)) //Absorbs existing intersecting intervals into the new intervals
                    {
                        timeInterval += intervalList[i];
                        intervalList.RemoveAt(i);
                    }
                }
                intervalList.Add(timeInterval);
                intervalList.Sort((t1, t2) => t1.Start <= t2.Start ? -1 : 1);
            }
        }

        public void Remove(TimeSpan start, TimeSpan end)
        {
            Remove(new TimeInterval(start, end));
        }

        public void Remove(TimeInterval timeInterval)
        {
            for (int i = intervalList.Count - 1; i >= 0; i--) //Loop is reversed to allow removing items from the list
            {
                TimeInterval currentInterval = intervalList[i];
                if (timeInterval.Contains(currentInterval))
                {
                    TimeInterval before, after;
                    before = new TimeInterval(currentInterval.Start, timeInterval.Start);
                    after = new TimeInterval(timeInterval.End, currentInterval.End);
                    intervalList.RemoveAt(i);
                    intervalList.Add(before);
                    intervalList.Add(after);
                }
                else if (timeInterval.Intersect(currentInterval))
                {
                    intervalList.RemoveAt(i);
                    intervalList.Add(currentInterval - timeInterval);
                }
            }
        }

        public TimeIntervalCollection Reverse()
        {
            TimeIntervalCollection complementary = new TimeIntervalCollection(Start, End);

            if (Count == 0)
            {
                complementary.Add(Start, End);
            }
            else
            {
                if (intervalList[0].Start > Start)
                    complementary.Add(Start, intervalList[0].Start);
                for (int i = 1; i < intervalList.Count; i++)
                {
                    complementary.Add(intervalList[i - 1].End, intervalList[i].Start);
                }
                if (intervalList[Count - 1].End != End)
                    complementary.Add(intervalList[^1].End, End);

            }

            return complementary;
        }

        public bool Contains(TimeSpan timeSpan)
        {
            foreach (var item in intervalList)
            {
                if (item.Contains(timeSpan)) return true;
            }

            return false;
        }

        /// <summary>
        /// If the TimeSpan is not contained in this collection, returns the End of the closest interval before the TimeSpan, otherwise returns the argument
        /// </summary>
        public TimeSpan GetClosestTimeSpanBefore(TimeSpan timeSpan)
        {
            if (Contains(timeSpan)) return timeSpan;

            //Since intervaList is sorted, the first intervalList before timeSpan is the right one
            for (int i = intervalList.Count; i >= 0 ; i--)
            {
                if (timeSpan > intervalList[i].End) return intervalList[i].End;
            }

            //timeSpan is not inside the collection
            if (timeSpan < Start) return ActualStart;
            return End;
        }

        /// <summary>
        /// If the TimeSpan is not contained in this collection, returns the Start of the closest interval after the TimeSpan, otherwise returns the argument
        /// </summary>
        public TimeSpan GetClosestTimeSpanAfter(TimeSpan timeSpan)
        {
            if (Contains(timeSpan)) return timeSpan;

            //Since intervaList is sorted, the first intervalList after timeSpan is the right one
            foreach (var timeInterval in intervalList)
            {
                if (timeSpan < timeInterval.Start) return timeInterval.Start;
            }

            //timeSpan is not inside the collection
            if (timeSpan < Start) return ActualStart;
            return End;
        }

        public IEnumerator<TimeInterval> GetEnumerator()
        {
            return intervalList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}