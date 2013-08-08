/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-22.js
 * @description Array.prototype.filter throws TypeError exception when 'length' is an object with toString and valueOf methods that don�t return primitive values
 */


function testcase() {

        var accessed = false;
        var firstStepOccured = false;
        var secondStepOccured = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
            return true;
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
            Array.prototype.filter.call(obj, callbackfn);
            return false;
        } catch (ex) {
            return (ex instanceof TypeError) && !accessed && firstStepOccured && secondStepOccured;
        }
    }
runTestCase(testcase);
