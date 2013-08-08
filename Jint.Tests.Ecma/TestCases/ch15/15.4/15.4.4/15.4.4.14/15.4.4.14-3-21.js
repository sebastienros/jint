/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-21.js
 * @description Array.prototype.indexOf - 'length' is an object that has an own valueOf method that returns an object and toString method that returns a string
 */


function testcase() {

        var toStringAccessed = false;
        var valueOfAccessed = false;

        var obj = {
            1: true,
            length: {
                toString: function () {
                    toStringAccessed = true;
                    return '2';
                },

                valueOf: function () {
                    valueOfAccessed = true;
                    return {};
                }
            }
        };

        return Array.prototype.indexOf.call(obj, true) === 1 && toStringAccessed && valueOfAccessed;
    }
runTestCase(testcase);
