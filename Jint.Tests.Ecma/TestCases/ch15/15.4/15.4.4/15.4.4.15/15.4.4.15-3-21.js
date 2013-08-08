/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-21.js
 * @description Array.prototype.lastIndexOf - 'length' is an object that has an own valueOf method that returns an object and toString method that returns a string
 */


function testcase() {

        var toStringAccessed = false;
        var valueOfAccessed = false;

        var targetObj = this;
        var obj = {
            1: targetObj,
            length: {
                toString: function () {
                    toStringAccessed = true;
                    return '3';
                },

                valueOf: function () {
                    valueOfAccessed = true;
                    return {};
                }
            }
        };

        return Array.prototype.lastIndexOf.call(obj, targetObj) === 1 && toStringAccessed && valueOfAccessed;
    }
runTestCase(testcase);
