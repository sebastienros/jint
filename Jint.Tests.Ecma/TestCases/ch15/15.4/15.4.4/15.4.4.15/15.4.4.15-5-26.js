/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-26.js
 * @description Array.prototype.lastIndexOf - side effects produced by step 2 are visible when an exception occurs
 */


function testcase() {

        var stepTwoOccurs = false;
        var stepFiveOccurs = false;
        var obj = {};

        Object.defineProperty(obj, "length", {
            get: function () {
                stepTwoOccurs = true;
                if (stepFiveOccurs) {
                    throw new Error("Step 5 occurred out of order");
                }
                return 20;
            },
            configurable: true
        });

        var fromIndex = {
            valueOf: function () {
                stepFiveOccurs = true;
                return 0;
            }
        };

        try {
            Array.prototype.lastIndexOf.call(obj, undefined, fromIndex);
            return stepTwoOccurs && stepFiveOccurs;
        } catch (ex) {
            return false;
        }
    }
runTestCase(testcase);
