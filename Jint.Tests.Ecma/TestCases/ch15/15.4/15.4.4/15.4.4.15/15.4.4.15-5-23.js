/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-23.js
 * @description Array.prototype.lastIndexOf - value of 'fromIndex' is an object that has an own valueOf method that returns an object and toString method that returns a string
 */


function testcase() {

        var toStringAccessed = false;
        var valueOfAccessed = false;

        var fromIndex = {
            toString: function () {
                toStringAccessed = true;
                return '1';
            },

            valueOf: function () {
                valueOfAccessed = true;
                return {};
            }
        };

        return [0, true].lastIndexOf(true, fromIndex) === 1 && toStringAccessed && valueOfAccessed;
    }
runTestCase(testcase);
