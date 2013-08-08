/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-24.js
 * @description Array.prototype.indexOf throws TypeError exception when value of 'fromIndex' is an object with toString and valueOf methods that don�t return primitive values
 */


function testcase() {

        var toStringAccessed = false;
        var valueOfAccessed = false;
        var fromIndex = {
            toString: function () {
                toStringAccessed = true;
                return {};
            },

            valueOf: function () {
                valueOfAccessed = true;
                return {};
            }
        };

        try {
            [0, true].indexOf(true, fromIndex);
            return false;
        } catch (e) {
            return toStringAccessed && valueOfAccessed;
        }
    }
runTestCase(testcase);
