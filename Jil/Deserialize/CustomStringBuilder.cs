﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Jil.Deserialize
{
    struct CustomStringBuilder
    {
        int BufferIx;
        char[] Buffer;

        // This method only works if the two arrays are 8-byte/4-char/1-long aligned (ie. size is a multiple; .NET handles
        //   putting them in the right alignment, we just have to guarantee the size)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe void ArrayCopyAligned(char[] smaller, char[] larger)
        {
            const int LongSizeShift = 2;

            fixed (char* fromPtrFixed = smaller)
            fixed (char* intoPtrFixed = larger)
            {
                var fromPtr = (long*)fromPtrFixed;
                var intoPtr = (long*)intoPtrFixed;

                var longLen = smaller.Length >> LongSizeShift;

                while (longLen > 0)
                {
                    *intoPtr = *fromPtr;
                    fromPtr++;
                    intoPtr++;
                    longLen--;
                }
            }
        }

        // This can handle arbitrary arrays, but is slower than ArrayCopyAligned
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe void ArrayCopy(char* fromPtrFixed, int fromLength, char* intoPtrFixed)
        {
            const int LongSizeShift = 2;
            const int NeedsIntCopy = 0x2;
            const int NeedsCharCopy = 0x1;

            var fromPtr = fromPtrFixed;
            var intoPtr = intoPtrFixed;

            var copyLongs = fromLength >> LongSizeShift;
            var fromLongPtr = (long*)fromPtr;
            var intoLongPtr = (long*)intoPtr;
            while (copyLongs > 0)
            {
                *intoLongPtr = *fromLongPtr;
                intoLongPtr++;
                fromLongPtr++;
                copyLongs--;
            }

            var copyInt = (fromLength & NeedsIntCopy) != 0;
            var copyChar = (fromLength & NeedsCharCopy) != 0;
            if (copyInt)
            {
                var fromIntPtr = (int*)fromLongPtr;
                var intoIntPtr = (int*)intoLongPtr;
                *intoIntPtr = *fromIntPtr;
                fromIntPtr++;
                intoIntPtr++;

                if (copyChar)
                {
                    var fromCharPtr = (char*)fromIntPtr;
                    var intoCharPtr = (char*)intoIntPtr;
                    *intoCharPtr = *fromCharPtr;
                }
            }
            else
            {
                if (copyChar)
                {
                    var fromCharPtr = (char*)fromLongPtr;
                    var intoCharPtr = (char*)intoLongPtr;
                    *intoCharPtr = *fromCharPtr;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AssureSpaceSmall(int neededSpace)
        {
            const int GrowthShift = 2;  // this grows by 4 char (8 byte) increments

            if (Buffer == null)
            {
                Buffer = new char[((neededSpace >> GrowthShift) + 1) << GrowthShift];
                return;
            }

            var desiredSize = BufferIx + neededSpace;

            if (Buffer.Length > desiredSize) return;

            var newBuffer = new char[((desiredSize >> GrowthShift) + 1) << GrowthShift];
            ArrayCopyAligned(Buffer, newBuffer);
            Buffer = newBuffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AssureSpaceLarge(int neededSpace)
        {
            const int GrowthShift = 7;  // this grows by 128 char (256 byte) increments

            if (Buffer == null)
            {
                Buffer = new char[((neededSpace >> GrowthShift) + 1) << GrowthShift];
                return;
            }

            var desiredSize = BufferIx + neededSpace;

            if (Buffer.Length > desiredSize) return;

            var newBuffer = new char[((desiredSize >> GrowthShift) + 1) << GrowthShift];
            ArrayCopyAligned(Buffer, newBuffer);
            Buffer = newBuffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AppendSmall(string str)
        {
            var newChars = str.Length;
            AssureSpaceSmall(newChars);

            fixed (char* fixedBufferPtr = Buffer)
            fixed (char* fixedStrPtr = str)
            {
                var copyInto = fixedBufferPtr + BufferIx;
                ArrayCopy(fixedStrPtr, newChars, copyInto);
            }

            BufferIx += str.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendSmall(char c)
        {
            AssureSpaceSmall(1);

            Buffer[BufferIx] = c;
            BufferIx++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AppendLarge(string str)
        {
            var newChars = str.Length;
            AssureSpaceLarge(newChars);

            fixed (char* fixedBufferPtr = Buffer)
            fixed (char* fixedStrPtr = str)
            {
                var copyInto = fixedBufferPtr + BufferIx;
                ArrayCopy(fixedStrPtr, newChars, copyInto);
            }

            BufferIx += str.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendLarge(char c)
        {
            AssureSpaceLarge(1);

            Buffer[BufferIx] = c;
            BufferIx++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AppendLarge(char[] chars, int start, int len)
        {
            var newChars = len;
            AssureSpaceLarge(newChars);

            fixed (char* fixedBufferPtr = Buffer)
            fixed (char* fixedCharsPtr = chars)
            {
                var bufferPtr = fixedBufferPtr + BufferIx;
                var strPtr = fixedCharsPtr + start;

                ArrayCopy(strPtr, len, bufferPtr);
            }

            BufferIx += len;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string StaticToString()
        {
            return new string(Buffer, 0, BufferIx);
        }

        public override string ToString()
        {
            if (Buffer == null) return "";

            return StaticToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            BufferIx = 0;
        }
    }
}