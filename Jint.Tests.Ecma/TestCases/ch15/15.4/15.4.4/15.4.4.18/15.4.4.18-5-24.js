/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-24.js
 * @description Array.prototype.forEach - string primitive can be used as thisArg
 */


function testcase() {

        var result = false;
        function callbackfn(val, idx, obj) {
            result = (this.valueOf() === "abc");
        }

        [11].forEach(callbackfn, "abc");
        return result;
    }
runTestCase(testcase);
