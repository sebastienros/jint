/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-4.js
 * @description Array.prototype.some - 'length' is an own data property that overrides an inherited data property on an array
 */


function testcase() {
        var arrProtoLen = 0;
        function callbackfn1(val, idx, obj) {
            return val > 10;
        }

        function callbackfn2(val, idx, obj) {
            return val > 11;
        }

        try {
            arrProtoLen = Array.prototype.length;
            Array.prototype.length = 0;
            Array.prototype[2] = 12;

            return [9, 11].some(callbackfn1) &&
                ![9, 11].some(callbackfn2);
        } finally {
            Array.prototype.length = arrProtoLen;
            delete Array.prototype[2];
        }
    }
runTestCase(testcase);
