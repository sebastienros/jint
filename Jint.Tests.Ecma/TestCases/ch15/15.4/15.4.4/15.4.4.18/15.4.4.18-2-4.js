/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-4.js
 * @description Array.prototype.forEach - 'length' is own data property that overrides an inherited data property on an Array
 */


function testcase() {
        var result = false;
        var arrProtoLen;
        function callbackfn(val, idx, obj) {
            result = (obj.length === 2);
        }

        try {
            arrProtoLen = Array.prototype.length;
            Array.prototype.length = 0;
            [12, 11].forEach(callbackfn);
            return result;
        } finally {
            Array.prototype.length = arrProtoLen;
        }

    }
runTestCase(testcase);
