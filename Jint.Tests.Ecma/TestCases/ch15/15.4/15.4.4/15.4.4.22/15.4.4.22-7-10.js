/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-7-10.js
 * @description Array.prototype.reduceRight - 'initialValue' is present
 */


function testcase() {

        var str = "initialValue is present";
        return str === [].reduceRight(function () { }, str);
    }
runTestCase(testcase);
