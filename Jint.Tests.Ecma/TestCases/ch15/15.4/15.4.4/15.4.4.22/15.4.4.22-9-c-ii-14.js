/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-14.js
 * @description Array.prototype.reduceRight - callbackfn uses arguments
 */


function testcase() {

        function callbackfn() {
            return arguments[0] === 100 && arguments[3][arguments[2]] === arguments[1];
        }

        return [11].reduceRight(callbackfn, 100) === true;
    }
runTestCase(testcase);
