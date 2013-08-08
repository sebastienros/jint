/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-7-11.js
 * @description Array.prototype.reduceRight - 'initialValue' is not present
 */


function testcase() {

        var str = "initialValue is not present";
        return str === [str].reduceRight(function () { });
    }
runTestCase(testcase);
