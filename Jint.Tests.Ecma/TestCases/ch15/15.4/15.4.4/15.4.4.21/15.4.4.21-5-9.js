/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-5-9.js
 * @description Array.prototype.reduce - 'initialValue' is returned if 'len' is 0 and 'initialValue' is present
 */


function testcase() {

        var accessed = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
        }

        return [].reduce(callbackfn, 3) === 3 && !accessed;
    }
runTestCase(testcase);
