/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-4.js
 * @description Array.prototype.filter - 'length' is own data property that overrides an inherited data property on an Array
 */


function testcase() {

        var arrProtoLen;

        function callbackfn(val, idx, obj) {
            return obj.length === 2;
        }

        try {
            arrProtoLen = Array.prototype.length;
            Array.prototype.length = 0;
            var newArr = [12, 11].filter(callbackfn);
            return newArr.length === 2;
        } finally {
            Array.prototype.length = arrProtoLen;
        }
    }
runTestCase(testcase);
