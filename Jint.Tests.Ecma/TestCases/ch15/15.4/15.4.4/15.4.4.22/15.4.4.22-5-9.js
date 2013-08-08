/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-5-9.js
 * @description Array.prototype.reduceRight - 'initialValue' is returned if 'len' is 0 and 'initialValue' is present
 */


function testcase() {

        var initialValue = 10;
        return initialValue === [].reduceRight(function () { }, initialValue);
    }
runTestCase(testcase);
