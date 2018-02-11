using System;
using System.Collections.Generic;

namespace Utilities.PriorityQueue
{
    public class PriorityQueue<T>
    {
        private List<PriorityQueueElement<T>> data;

        public PriorityQueue()
        {
            this.data = new List<PriorityQueueElement<T>>();
        }

        // adds highest priority in the end
        // code from: https://visualstudiomagazine.com/articles/2012/11/01/priority-queues-with-c/listing3.aspx
        public virtual void Enqueue(T item, float priority)
        {
            PriorityQueueElement<T> pqe = new PriorityQueueElement<T>(item, priority);
            // add item
            data.Add(pqe);
            int ci = data.Count - 1;
            // swap items to maintain priority
            while (ci > 0)
            {
                int pi = (ci - 1) / 2;
                if (data[ci].ComparePriority(data[pi]) >= 0)
                    break;
                PriorityQueueElement<T> tmp = data[ci];
                data[ci] = data[pi];
                data[pi] = tmp;
                ci = pi;
            }
        }

        // this dequeue method results in queue 'travelling' in memory as it grows and items are popped
        public T DequeueBad()
        {
            if (IsEmpty())
                throw new IndexOutOfRangeException("Priority Queue: attempting to pop from an empty queue.");
            PriorityQueueElement<T> item = data[0];
            data.RemoveAt(0);
            return item.item;
        }

        // code from https://visualstudiomagazine.com/articles/2012/11/01/priority-queues-with-c/listing4.aspx
        public virtual T Dequeue()
        {
            // assumes pq is not empty; up to calling code
            int li = data.Count - 1; // last index (before removal)
            PriorityQueueElement<T> frontItem = data[0];   // fetch the front
            data[0] = data[li];
            data.RemoveAt(li);

            --li; // last index (after removal)
            int pi = 0; // parent index. start at front of pq
            while (true)
            {
                int ci = pi * 2 + 1; // left child index of parent
                if (ci > li) break;  // no children so done
                int rc = ci + 1;     // right child
                if (rc <= li && data[rc].ComparePriority(data[ci]) < 0) // if there is a rc (ci + 1), and it is smaller than left child, use the rc instead
                    ci = rc;
                if (data[pi].ComparePriority(data[ci]) <= 0) break; // parent is smaller than (or equal to) smallest child so done
                PriorityQueueElement<T> tmp = data[pi]; data[pi] = data[ci]; data[ci] = tmp; // swap parent and child
                pi = ci;
            }
            return frontItem.item;
        }

        public bool IsEmpty()
        {
            if (this.data.Count == 0)
                return true;
            else
                return false;
        }

        public int Count()
        {
            return data.Count;
        }

        public struct PriorityQueueElement<T>
        {
            public float priority { get; set; }
            public T item { get; set; }
            public PriorityQueueElement(T item, float priority)
            {
                this.priority = priority;
                this.item = item;
            }
            // for priority queue
            public int ComparePriority(PriorityQueueElement<T> pt)
            {
                if (this.priority < pt.priority)
                    return -1;
                else if (this.priority == pt.priority)
                    return 0;
                else
                    return 1;
            }
        }
    }
}
