import React, { PropTypes } from "react";
import GridCell from "dnn-grid-cell";
import BasicPackageInformation from "../common/BasicPackageInformation";
import Switch from "dnn-switch";
import Button from "dnn-button";
import DropdownWithError from "dnn-dropdown-with-error";
import MultiLineInput from "dnn-multi-line-input";
import SingleLineInputWithError from "dnn-single-line-input-with-error";
import Localization from "localization";

const StepThree = ({packageManifest, onCancel, onNext, onBasePathChange, onPrevious, onFileOrAssemblyChange}) => (
    <GridCell className="review-assemblies-step">
        <h6 className="box-title">{Localization.get("CreatePackage_ChooseAssemblies.Label")}</h6>
        <p className="box-subtitle">{Localization.get("CreatePackage_ChooseAssemblies.HelpText")}</p>
        <GridCell className="package-assemblies-container no-padding">
            <MultiLineInput
                className="package-assemblies"
                value={packageManifest.assemblies.join("\n")}
                onChange={onFileOrAssemblyChange.bind(this, "assemblies")}
                />
        </GridCell>
        <GridCell className="modal-footer">
            <Button type="secondary" onClick={onCancel}>Cancel</Button>
            <Button type="secondary" onClick={onPrevious}>Previous</Button>
            <Button type="primary" onClick={onNext}>Next</Button>
        </GridCell>
    </GridCell>
);

StepThree.propTypes = {
    packageManifest: PropTypes.object,
    onCancel: PropTypes.func,
    onNext: PropTypes.func,
    onBasePathChange: PropTypes.func,
    onPrevious: PropTypes.func,
    onFileOrAssemblyChange: PropTypes.func
};
export default StepThree;