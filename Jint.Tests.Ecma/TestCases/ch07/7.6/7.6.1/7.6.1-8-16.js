/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-8-16.js
 * @description Allow reserved words as property names by set function within an object, accessed via indexing: undefined, NaN, Infinity
 */


function testcase() {
        var test0 = 0, test1 = 1, test2 = 2;
        var tokenCodes  = {
            set undefined(value){
                test0 = value;
            },
            get undefined(){
                return test0;
            },
            set NaN(value){
                test1 = value;
            },
            get NaN(){
                return test1;
            },
            set Infinity(value){
                test2 = value;
            },
            get Infinity(){
                return test2;
            }
        }; 
        var arr = [
            'undefined',
            'NaN',
            'Infinity'
        ];
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
