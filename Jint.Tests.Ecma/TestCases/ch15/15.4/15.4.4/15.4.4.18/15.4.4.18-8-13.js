/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-13.js
 * @description Array.prototype.forEach - undefined will be returned when 'len' is 0
 */


function testcase() {

        var accessed = false;
        function callbackfn(val, idx, obj) {
            accessed = true;
        }

        var result = [].forEach(callbackfn);
        return typeof result === "undefined" && !accessed;
    }
runTestCase(testcase);
