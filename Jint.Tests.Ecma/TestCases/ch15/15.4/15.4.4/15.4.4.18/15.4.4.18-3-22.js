/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-22.js
 * @description Array.prototype.forEach throws TypeError exception when 'length' is an object with toString and valueOf methods that don�t return primitive values
 */


function testcase() {

        var accessed = false;
        var firstStepOccured = false;
        var secondStepOccured = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
        }

        var obj = {
            1: 11,
            2: 12,

            length: {
                valueOf: function () {
                    firstStepOccured = true;
                    return {};
                },
                toString: function () {
                    secondStepOccured = true;
                    return {};
                }
            }
        };

        try {
            Array.prototype.forEach.call(obj, callbackfn);
            return false;
        } catch (ex) {
            return ex instanceof TypeError && !accessed;
        }
    }
runTestCase(testcase);
