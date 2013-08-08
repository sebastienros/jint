/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-4.js
 * @description Array.prototype.every - 'length' is own data property that overrides an inherited data property on an Array
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
            Array.prototype[2] = 9;

            return [12, 11].every(callbackfn1) &&
                ![12, 11].every(callbackfn2);
        } finally {
            Array.prototype.length = arrProtoLen;
            delete Array.prototype[2];
        }

    }
runTestCase(testcase);
