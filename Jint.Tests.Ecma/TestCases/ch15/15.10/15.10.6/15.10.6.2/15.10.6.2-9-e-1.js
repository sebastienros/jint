/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.6/15.10.6.2/15.10.6.2-9-e-1.js
 * @description RegExp.prototype.exec - the removed step 9.e won't affected current algorithm
 */


function testcase() {
        var str = "Hello World!";
        var regObj = new RegExp("World");
        var result = false;
        result = regObj.exec(str).toString() === "World";
        return result;
    }
runTestCase(testcase);
