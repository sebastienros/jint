/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-28.js
 * @description Array.prototype.reduce - applied to String object, which implements its own property get method
 */


function testcase() {

        var testResult = false;
        var initialValue = 0;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (curVal === "1");
            }
        }

        var str = new String("012");
     
        Array.prototype.reduce.call(str, callbackfn, initialValue);
        return testResult;
        
    }
runTestCase(testcase);
