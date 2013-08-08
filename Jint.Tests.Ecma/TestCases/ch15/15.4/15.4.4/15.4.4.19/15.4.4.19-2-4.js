/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-4.js
 * @description Array.prototype.map - when 'length' is own data property that overrides an inherited data property on an Array
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return val > 10;
        }
        var arrProtoLen;
        try {
            arrProtoLen = Array.prototype.length;
            Array.prototype.length = 0;
            var testResult = [12, 11].map(callbackfn);
            return testResult.length === 2;
        } finally {
            Array.prototype.length = arrProtoLen;
        }
    }
runTestCase(testcase);
