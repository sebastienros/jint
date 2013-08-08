/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-22.js
 * @description Array.prototype.indexOf - value of 'fromIndex' is an Object, which has an own valueOf method
 */


function testcase() {

        var fromIndex = {
            valueOf: function () {
                return 1;
            }
        };


        return [0, true].indexOf(true, fromIndex) === 1;
    }
runTestCase(testcase);
