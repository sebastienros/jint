/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-3.js
 * @description Array.prototype.indexOf applied to boolean primitive
 */


function testcase() {
        var targetObj = {};
        try {
            Boolean.prototype[1] = targetObj;
            Boolean.prototype.length = 2;

            return Array.prototype.indexOf.call(true, targetObj) === 1;
        } finally {
            delete Boolean.prototype[1];
            delete Boolean.prototype.length;
        }
    }
runTestCase(testcase);
