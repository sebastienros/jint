/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-22.js
 * @description Array.prototype.map throws TypeError exception when 'length' is an object with toString and valueOf methods that don�t return primitive values
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return val > 10;
        }

        var obj = {
            1: 11,
            2: 12,

            length: {
                valueOf: function () {
                    return {};
                },
                toString: function () {
                    return {};
                }
            }
        };

        try {
            Array.prototype.map.call(obj, callbackfn);
            return false;
        } catch (ex) {
            return ex instanceof TypeError;
        }
    }
runTestCase(testcase);
