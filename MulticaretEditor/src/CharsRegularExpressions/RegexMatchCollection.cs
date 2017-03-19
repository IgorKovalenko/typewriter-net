//------------------------------------------------------------------------------
// <copyright file="RegexMatchCollection.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

// The MatchCollection lists the successful matches that
// result when searching a string for a regular expression.

namespace CharsRegularExpressions {

    using System.Collections;
    using System.Collections.Generic;


    /*
     * This collection returns a sequence of successful match results, either
     * from GetMatchCollection() or GetExecuteCollection(). It stops when the
     * first failure is encountered (it does not return the failed match).
     */
    /// <devdoc>
    ///    <para>
    ///       Represents the set of names appearing as capturing group
    ///       names in a regular expression.
    ///    </para>
    /// </devdoc>
    public class MatchCollection : ICollection {
        internal Regex _regex;
        internal ArrayList _matches;
        internal bool _done;
        internal char[] _input;
        internal int _beginning;
        internal int _length;
        internal int _startat;
        internal int _prevlen;

        private static int infinite = 0x7FFFFFFF;

        /*
         * Nonpublic constructor
         */
        internal MatchCollection(Regex regex, char[] input, int beginning, int length, int startat) {
            if (startat < 0 || startat > input.Length)
                throw new System.ArgumentOutOfRangeException("startat", SR.GetString(SR.BeginIndexNotNegative));

            _regex = regex;
            _input = input;
            _beginning = beginning;
            _length = length;
            _startat = startat;
            _prevlen = -1;
            _matches = new ArrayList();
            _done = false;
        }

        internal Match GetMatch(int i) {
            if (i < 0)
                return null;

            if (_matches.Count > i)
                return (Match)_matches[i];

            if (_done)
                return null;

            Match match;

            do {
                match = _regex.Run(false, _prevlen, _input, _beginning, _length, _startat);

                if (!match.Success) {
                    _done = true;
                    return null;
                }

                _matches.Add(match);

                _prevlen = match._length;
                _startat = match._textpos;

            } while (_matches.Count <= i);

            return match;
        }

        /// <devdoc>
        ///    <para>
        ///       Returns the number of captures.
        ///    </para>
        /// </devdoc>
        public int Count {
            get {
                if (_done)
                    return _matches.Count;

                GetMatch(infinite);

                return _matches.Count;
            }
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public object SyncRoot {
            get {
                return this;
            }
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public bool IsSynchronized {
            get {
                return false;
            }
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public bool IsReadOnly {
            get {
                return true;
            }
        }


        /// <devdoc>
        ///    <para>
        ///       Returns the ith Match in the collection.
        ///    </para>
        /// </devdoc>
        public virtual Match this[int i]
        {
            get {
                Match match;

                match = GetMatch(i);

                if (match == null)
                    throw new System.ArgumentOutOfRangeException("i");

                return match;
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Copies all the elements of the collection to the given array
        ///       starting at the given index.
        ///    </para>
        /// </devdoc>
        public void CopyTo(System.Array array, int arrayIndex) {
            if ((array != null) && (array.Rank != 1))
            {
                throw new System.ArgumentException(SR.GetString(SR.Arg_RankMultiDimNotSupported));
            }

            // property access to force computation of whole array
            int count = Count;
            try
            {
                _matches.CopyTo(array, arrayIndex);
            }
            catch (System.ArrayTypeMismatchException ex)
            {
                throw new System.ArgumentException(SR.GetString(SR.Arg_InvalidArrayType), ex);
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Provides an enumerator in the same order as Item[i].
        ///    </para>
        /// </devdoc>
        public IEnumerator GetEnumerator() {
            return new MatchEnumerator(this);
        }
    }

    /*
     * This non-public enumerator lists all the group matches.
     * Should it be public?
     */
    internal class MatchEnumerator : IEnumerator {
        internal MatchCollection _matchcoll;
        internal Match _match = null;
        internal int _curindex;
        internal bool _done;

        /*
         * Nonpublic constructor
         */
        internal MatchEnumerator(MatchCollection matchcoll) {
            _matchcoll = matchcoll;
        }

        /*
         * Advance to the next match
         */
        public bool MoveNext() {
            if (_done)
                return false;

            _match = _matchcoll.GetMatch(_curindex);
            _curindex++;

            if (_match == null) {
                _done = true;
                return false;
            }

            return true;
        }

        /*
         * The current match
         */
        public object Current {
            get { 
                if (_match == null)
                    throw new System.InvalidOperationException(SR.GetString(SR.EnumNotStarted));
                return _match;
            }
        }

        /*
         * Position before the first item
         */
        public void Reset() {
            _curindex = 0;
            _done = false;
            _match = null;
        }
    }




}