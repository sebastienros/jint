/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-24.js
 * @description Array.prototype.lastIndexOf throws TypeError exception when value of 'fromIndex' is an object that both toString and valueOf methods than don't return primitive value
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
            [0, null].lastIndexOf(null, fromIndex);
            return false;
        } catch (e) {
            return toStringAccessed && valueOfAccessed;
        }
    }
runTestCase(testcase);
