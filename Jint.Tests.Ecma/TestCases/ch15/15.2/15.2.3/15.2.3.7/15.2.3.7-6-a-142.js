/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-142.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is the length property of 'O', test the [[Value]] field of 'desc' is an Object which has an own toString method (15.4.5.1 step 3.c)
 */


function testcase() {

        var arr = [];

        Object.defineProperties(arr, {
            length: {
                value: {
                    toString: function () {
                        return '2';
                    }
                }
            }
        });

        return arr.length === 2;
    }
runTestCase(testcase);
