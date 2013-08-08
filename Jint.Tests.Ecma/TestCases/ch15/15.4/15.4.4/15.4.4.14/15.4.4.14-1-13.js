/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-13.js
 * @description Array.prototype.indexOf applied to the JSON object
 */


function testcase() {
        var targetObj = {};
        try {
            JSON[3] = targetObj;
            JSON.length = 5;
            return Array.prototype.indexOf.call(JSON, targetObj) === 3;
        } finally {
            delete JSON[3];
            delete JSON.length;
        }
    }
runTestCase(testcase);
