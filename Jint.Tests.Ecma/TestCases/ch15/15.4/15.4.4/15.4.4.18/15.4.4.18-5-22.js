/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-22.js
 * @description Array.prototype.forEach - boolean primitive can be used as thisArg
 */


function testcase() {

        var result = false;

        function callbackfn(val, idx, obj) {
            result = (this.valueOf() === false);
        }

        [11].forEach(callbackfn, false);
        return result;
    }
runTestCase(testcase);
