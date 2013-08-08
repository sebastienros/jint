/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-7.js
 * @description Array.prototype.every - element to be retrieved is inherited data property on an Array-like object
 */


function testcase() {

        var kValue = 'abc';

        function callbackfn(val, idx, obj) {
            if (idx === 5) {
                return val !== kValue;
            } else {
                return true;
            }
        }

        var proto = { 5: kValue };

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child.length = 10;

        return !Array.prototype.every.call(child, callbackfn);
    }
runTestCase(testcase);
