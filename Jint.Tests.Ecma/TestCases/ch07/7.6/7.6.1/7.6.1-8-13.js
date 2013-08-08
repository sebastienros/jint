/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-8-13.js
 * @description Allow reserved words as property names by set function within an object, accessed via indexing: implements, let, private
 */


function testcase() {
        var test0 = 0, test1 = 1, test2 = 2;
        var tokenCodes  = {
            set implements(value){
                test0 = value;
            },
            get implements(){
                return test0;
            },
            set let(value){
                test1 = value;
            },
            get let(){
                return test1
            },
            set private(value){
                test2 = value;
            },
            get private(){
                return test2;
            }
        }; 
        var arr = [
            'implements',
            'let',
            'private'
        ];
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
