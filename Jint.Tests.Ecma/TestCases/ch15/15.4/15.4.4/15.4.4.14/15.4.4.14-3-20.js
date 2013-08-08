/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-20.js
 * @description Array.prototype.indexOf - value of 'length' is an Object which has an own valueOf method.
 */


function testcase() {

        //valueOf method will be invoked first, since hint is Number
        var obj = {
            1: true,
            2: 2,
            length: {
                valueOf: function () {
                    return 2;
                }
            }
        };

        return Array.prototype.indexOf.call(obj, true) === 1 &&
            Array.prototype.indexOf.call(obj, 2) === -1;
    }
runTestCase(testcase);
