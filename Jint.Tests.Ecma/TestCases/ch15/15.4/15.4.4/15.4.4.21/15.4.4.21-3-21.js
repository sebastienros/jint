/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-21.js
 * @description Array.prototype.reduce - 'length' is an object that has an own valueOf method that returns an object and toString method that returns a string
 */


function testcase() {

        var valueOfOccured = false;
        var toStringOccured = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            return (curVal === 11 && idx === 1);
        }

        var obj = {
            1: 11,
            2: 9,
            length: {
                valueOf: function () {
                    valueOfOccured = true;
                    return {};
                },
                toString: function () {
                    toStringOccured = true;
                    return '2';
                }
            }
        };

        return Array.prototype.reduce.call(obj, callbackfn, 1) === true && valueOfOccured && toStringOccured;
    }
runTestCase(testcase);
