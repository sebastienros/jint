/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-21.js
 * @description Array.prototype.filter - 'length' is an object that has an own valueOf method that returns an object and toString method that returns a string
 */


function testcase() {

        var firstStepOccured = false;
        var secondStepOccured = false;

        function callbackfn(val, idx, obj) {
            return true;
        }

        var obj = {
            1: 11,
            2: 9,
            length: {
                valueOf: function () {
                    firstStepOccured = true;
                    return {};
                },
                toString: function () {
                    secondStepOccured = true;
                    return '2';
                }
            }
        };

        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return newArr.length === 1 && newArr[0] === 11 && firstStepOccured && secondStepOccured;
    }
runTestCase(testcase);
