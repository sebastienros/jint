/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-4-12.js
 * @description Array.prototype.forEach - 'callbackfn' is a function
 */


function testcase() {

        var accessed = false;
        function callbackfn(val, idx, obj) {
            accessed = true;
        }

        [11, 9].forEach(callbackfn);
        return accessed;
    }
runTestCase(testcase);
