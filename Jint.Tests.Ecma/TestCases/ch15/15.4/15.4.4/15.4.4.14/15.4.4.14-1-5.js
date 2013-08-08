/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-5.js
 * @description Array.prototype.indexOf applied to number primitive
 */


function testcase() {
        var targetObj = {};
        try {
            Number.prototype[1] = targetObj;
            Number.prototype.length = 2;

            return Array.prototype.indexOf.call(5, targetObj) === 1;
        } finally {
            delete Number.prototype[1];
            delete Number.prototype.length;
        }
    }
runTestCase(testcase);
