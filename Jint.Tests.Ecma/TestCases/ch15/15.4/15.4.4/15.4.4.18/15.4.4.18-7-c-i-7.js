/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-7.js
 * @description Array.prototype.forEach - element to be retrieved is inherited data property on an Array-like object
 */


function testcase() {

        var kValue = 'abc';
        var testResult = false;

        function callbackfn(val, idx, obj) {
            if (idx === 5) {
                testResult = (val === kValue);
            }
        }

        var proto = { 5: kValue };

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child.length = 10;

        Array.prototype.forEach.call(child, callbackfn);

        return testResult;
    }
runTestCase(testcase);
