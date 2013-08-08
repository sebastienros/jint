/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-2.js
 * @description Array.prototype.forEach - 'length' is own data property on an Array
 */


function testcase() {
        var result = false;
        function callbackfn(val, idx, obj) {
            result = (obj.length === 2);
        }

        [12, 11].forEach(callbackfn);
        return result;
    }
runTestCase(testcase);
