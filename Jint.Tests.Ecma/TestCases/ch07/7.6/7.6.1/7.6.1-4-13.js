/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-4-13.js
 * @description Allow reserved words as property names by set function within an object, verified with hasOwnProperty: implements, let, private
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
        for(var p in tokenCodes) {       
            for(var p1 in arr) {                
                if(arr[p1] === p) {
                    if(!tokenCodes.hasOwnProperty(arr[p1])) {
                        return false;
                    };
                }
            }
        }
        return true;
    }
runTestCase(testcase);
